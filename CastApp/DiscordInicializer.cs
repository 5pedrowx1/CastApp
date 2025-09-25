using Discord;
using Discord.WebSocket;

namespace CastApp
{
    public class DiscordInicializer
    {
        public static DiscordSocketClient? _client;

        public static async Task InitializeDiscord()
        {
            var Config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.Guilds
            };

            _client = new DiscordSocketClient(Config);

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;

            await _client.LoginAsync(TokenType.Bot, "");

            await _client.StartAsync();
            await Task.Delay(Timeout.Infinite);
        }

        private static Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private static Task ReadyAsync()
        {
            Console.WriteLine($"{_client?.CurrentUser} está conectado!");
            return Task.CompletedTask;
        }

        private static async Task MessageReceivedAsync(SocketMessage message)
        {
            //TODO: Adicionar codigo aqui
        }
    }
}

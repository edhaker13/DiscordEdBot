namespace DiscordEdBot
{
    using System.Threading.Tasks;
    class Program
    {
        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();
        public async Task MainAsync()
        {
            var clientConfig = new Discord.WebSocket.DiscordSocketConfig { LogLevel = Discord.LogSeverity.Info, };
            using (var socketClient = new Discord.WebSocket.DiscordSocketClient(clientConfig))
            {
                var commandService = new Discord.Commands.CommandService(new Discord.Commands.CommandServiceConfig { LogLevel = clientConfig.LogLevel, });
                var dependencyMap = new Discord.Commands.DependencyMap();
                dependencyMap.Add(new MusicService());
                // Hook our methods event handlers.
                socketClient.Log += MainHandler.LogAsync;
                socketClient.MessageReceived += (message) => MainHandler.MessageReceivedAsync(message, commandService, dependencyMap);
                // Hook events and modules to CommandService.
                await MainHandler.RegisterCommandService(commandService).ConfigureAwait(false);
                // Discord Login Token, very secret. TODO: Store value securely.
                const string botToken = "MzEwMTA3NDgyNDE4ODM5NTYy.C-5WSg.zKfI3vvzB_PYmC8bYH2ODMtYpU4";
                await socketClient.LoginAsync(Discord.TokenType.Bot, botToken).ConfigureAwait(false);
                // Attempt connection to discord (handles retries).
                await socketClient.StartAsync().ConfigureAwait(false);

                // Keep runnning unless an external force causes a shutdown.
                await Task.Delay(-1);
            }
        }
    }
}
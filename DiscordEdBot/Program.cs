namespace DiscordEdBot
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using Microsoft.Extensions.DependencyInjection;

    internal class Program
    {
        public static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            var clientConfig = new DiscordSocketConfig {LogLevel = LogSeverity.Info};
            using (var socketClient = new DiscordSocketClient(clientConfig))
            {
                var commandService = new CommandService(new CommandServiceConfig {LogLevel = clientConfig.LogLevel});
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(socketClient);
                serviceCollection.AddSingleton(new MusicService());
                var serviceProvider = serviceCollection.BuildServiceProvider();
                // Hook our methods event handlers.
                socketClient.Log += MainHandler.LogAsync;
                socketClient.MessageReceived +=
                    message => MainHandler.MessageReceivedAsync(message, commandService, serviceProvider);
                // Hook events and modules to CommandService.
                await MainHandler.RegisterCommandService(commandService).ConfigureAwait(false);
                // Discord Login Token, very secret. TODO: Store value securely.
                const string botToken = "MzEwMTA3NDgyNDE4ODM5NTYy.C-5WSg.zKfI3vvzB_PYmC8bYH2ODMtYpU4";
                await socketClient.LoginAsync(TokenType.Bot, botToken).ConfigureAwait(false);
                // Attempt connection to discord (handles retries).
                await socketClient.StartAsync().ConfigureAwait(false);

                // Keep runnning unless an external force causes a shutdown.
                await Task.Delay(-1);
            }
        }
    }
}
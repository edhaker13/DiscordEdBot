namespace DiscordEdBot
{
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class LoginConfig
    {
        public TokenType TokenType { get; set; }
        public string Token { get; set; }
        public bool ValidateToken { get; set; } = true;
    }
    public class DiscordEdBotConfig
    {
        public DiscordEdBotConfig() {}
        public DiscordEdBotConfig(IConfiguration configuration)
        {
            configuration.Bind(nameof(DiscordEdBot), this);
        }
        public DiscordSocketConfig SocketClient { get; set; }
        public CommandServiceConfig CommandService { get; set; }
        public LoginConfig Login { get; set; }
    }

    internal class Program
    {
        public static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                // TODO: Use user secrets or other non-local source.
                .Build();
            var botConfig = new DiscordEdBotConfig(config);
            DiscordSocketClient socketClient = null;
            MusicService musicService = null;
            try
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton<IConfiguration>(config);
                serviceCollection.AddSingleton(botConfig);
                socketClient = new DiscordSocketClient(botConfig.SocketClient);
                serviceCollection.AddSingleton(socketClient);
                serviceCollection.AddSingleton(musicService = new MusicService());
                var serviceProvider = serviceCollection.BuildServiceProvider();
                var commandService = new CommandService(botConfig.CommandService);
                // Hook our methods event handlers.
                socketClient.Log += MainHandler.LogAsync;
                socketClient.MessageReceived +=
                    message => MainHandler.MessageReceivedAsync(message, commandService, serviceProvider);
                // Hook events and modules to CommandService.
                await MainHandler.RegisterCommandService(commandService).ConfigureAwait(false);
                await socketClient.LoginAsync(botConfig.Login.TokenType, botConfig.Login.Token, botConfig.Login.ValidateToken).ConfigureAwait(false);
                // Attempt connection to discord (handles retries).
                await socketClient.StartAsync().ConfigureAwait(false);

                // Keep runnning unless an external force causes a shutdown.
                await Task.Delay(-1);
            }
            finally
            {
                musicService?.Dispose();
                socketClient?.Dispose();
            }
        }
    }
}
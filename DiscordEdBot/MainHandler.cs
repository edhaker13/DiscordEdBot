namespace DiscordEdBot
{
    using Discord;
    using Discord.Commands;
    using System.Threading.Tasks;
    static class MainHandler
    {
        public static Task LogAsync(LogMessage message)
        {
            var originalColor = System.Console.ForegroundColor;
            var newColor = originalColor;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    newColor = System.ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    newColor = System.ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    newColor = System.ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    newColor = System.ConsoleColor.DarkGray;
                    break;
            }
            System.Console.ForegroundColor = newColor;
            System.Console.WriteLine($"{System.DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message}");
            System.Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }
        public static async Task MessageReceivedAsync(Discord.WebSocket.SocketMessage message, CommandService commandService, IDependencyMap dependencyMap = null)
        {
            // Ignore non-user messages.
            var userMessage = message as Discord.WebSocket.SocketUserMessage;
            if (userMessage == null) return;
            int prefixEndPosition = 0;
            // TODO: Configurable prefix(es).
            if (userMessage.HasCharPrefix('!', ref prefixEndPosition) ||
                userMessage.HasCharPrefix('~', ref prefixEndPosition) ||
                userMessage.HasMentionPrefix(message.Discord.CurrentUser, ref prefixEndPosition))
            {
                var commandContext = new SocketCommandContext(message.Discord, userMessage);
                var commandResult = await commandService.ExecuteAsync(commandContext, prefixEndPosition, dependencyMap);
            }
        }
        public static async Task RegisterCommandService(CommandService commandService)
        {
            commandService.Log += LogAsync;
            await commandService.AddModulesAsync(System.Reflection.Assembly.GetEntryAssembly()).ConfigureAwait(false);
        }
    }
}

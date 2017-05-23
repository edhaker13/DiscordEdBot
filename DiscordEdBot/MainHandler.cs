namespace DiscordEdBot
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using Microsoft.Extensions.DependencyInjection;

    internal static class MainHandler
    {
        public static Task LogAsync(LogMessage message)
        {
            var originalColor = Console.ForegroundColor;
            ConsoleColor newColor;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    newColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    newColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    newColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    newColor = ConsoleColor.DarkGray;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            Console.ForegroundColor = newColor;
            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message}");
            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }

        public static async Task MessageReceivedAsync(SocketMessage message, CommandService commandService,
            IServiceProvider serviceProvider = null)
        {
            // Ignore non-user messages.
            var userMessage = message as SocketUserMessage;
            if (userMessage == null) return;
            var client = serviceProvider.GetService<DiscordSocketClient>();
            var prefixEndPosition = 0;
            // TODO: Configurable prefix(es).
            if (userMessage.HasCharPrefix('!', ref prefixEndPosition) ||
                userMessage.HasCharPrefix('~', ref prefixEndPosition) ||
                userMessage.HasMentionPrefix(client.CurrentUser, ref prefixEndPosition))
            {
                var commandContext = new SocketCommandContext(client, userMessage);
                var unused = await commandService.ExecuteAsync(commandContext, prefixEndPosition, serviceProvider);
            }
        }

        public static async Task RegisterCommandService(CommandService commandService)
        {
            commandService.Log += LogAsync;
            await commandService.AddModulesAsync(Assembly.GetEntryAssembly()).ConfigureAwait(false);
        }
    }
}
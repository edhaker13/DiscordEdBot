namespace DiscordEdBot
{
    using System.Threading.Tasks;
    using Discord.Commands;

    public class ModuleMisc : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        public Task PingAsync()
        {
            const string reply = "pong!";
            return ReplyAsync(reply);
        }

        [Command("echo")]
        public Task EchoAsync([Remainder] string text)
        {
            return ReplyAsync(text);
        }
    }
}
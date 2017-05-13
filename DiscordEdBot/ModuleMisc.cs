namespace DiscordEdBot
{
    using Discord.Commands;
    using System.Threading.Tasks;

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

namespace DiscordEdBot
{
    using Discord.Commands;
    using System.Threading.Tasks;
    public class ModuleMusic : ModuleBase<SocketCommandContext>
    {
        public MusicService MusicService { get; set; }
        [Command("play")]
        public Task PlayAsync(string path = null)
        {
            return Task.Factory.StartNew(() => MusicService.PlayAsync(Context, path));
        }
    }
}

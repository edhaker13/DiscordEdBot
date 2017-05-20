namespace DiscordEdBot
{
    using System.Threading.Tasks;
    using Discord.Commands;

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
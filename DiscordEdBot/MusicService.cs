namespace DiscordEdBot
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Audio;
    using Discord.Commands;

    public class MusicService : IDisposable
    {
        private IAudioClient _audioClient;

        public void Dispose()
        {
            _audioClient.Dispose();
        }

        private static Process InputToPcm(string input)
        {
            var ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -i \"{input}\" -ac 2 -ar 48000 -f s16le pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var procces = Process.Start(ffmpeg);
            return procces;
        }

        private static Process DecryptUrl(string url)
        {
            var ytdl = new ProcessStartInfo
            {
                FileName = "youtube-dl",
                Arguments = $"-g -f bestaudio \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var process = Process.Start(ytdl);
            return process;
        }

        public async Task JoinChannelAsync(SocketCommandContext context, IVoiceChannel channel = null)
        {
            channel = channel ?? (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null)
            {
                await context.Channel.SendMessageAsync("No valid voice channel to join found.");
                return;
            }
            if (_audioClient != null) await _audioClient.StopAsync().ConfigureAwait(false);
            _audioClient = await channel.ConnectAsync().ConfigureAwait(false);
        }

        // TODO: Control playback (info, next, stop, pause?).
        public async Task PlayAsync(SocketCommandContext context, string path = null)
        {
            if (_audioClient == null)
            {
                await JoinChannelAsync(context).ConfigureAwait(false);
                if (_audioClient == null)
                    return;
            }
            // TODO: Check if another song is playing / Implement queue-playlist.
            if (File.Exists(path))
                await PlayFileAsync(context, path).ConfigureAwait(false);
            else
                await PlayUrlAsync(context, path).ConfigureAwait(false);
            await context.Channel.SendMessageAsync("Finished playing music!").ConfigureAwait(false);
        }

        private async Task PlayFileAsync(SocketCommandContext context, string path)
        {
            await context.Channel.SendMessageAsync($"Playing song: '{path}'...").ConfigureAwait(false);
            if (path.EndsWith("opus"))
                await PlayAsOpusAsync(path);
            else
                await PlayAsPcmAsync(path, InputToPcm);
        }

        private async Task PlayUrlAsync(SocketCommandContext context, string url)
        {
            var process = DecryptUrl(url);
            var decrypted = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var urls = decrypted.Split(Environment.NewLine.ToCharArray());
            if (urls.Length == 0)
            {
                await context.Channel.SendMessageAsync($"Failed to play link.").ConfigureAwait(false);
                return;
            }
            foreach (var newUrl in urls)
                //await context.Channel.SendMessageAsync($"Playing link as music: {newUrl}").ConfigureAwait(false);
                await PlayAsPcmAsync(newUrl, InputToPcm).ConfigureAwait(false);
        }

        private async Task PlayAsOpusAsync(string path)
        {
            using (var source = File.OpenRead(path))
            using (var destination = _audioClient.CreateOpusStream(1920)
            ) // TODO: Wait for DiscordNet to fix opus streams.
            {
                if (!source.CanRead) return;
                await source.CopyToAsync(destination).ConfigureAwait(false);
                await destination.FlushAsync().ConfigureAwait(false);
            }
        }

        private async Task PlayAsPcmAsync(string path, Func<string, Process> convertProcess)
        {
            using (var process = convertProcess(path)) // TODO: Skip conversion if possible
            using (var source = process.StandardOutput.BaseStream)
            using (var destination = _audioClient.CreatePCMStream(AudioApplication.Music, 1920))
            {
                if (process.HasExited) return;
                await source.CopyToAsync(destination).ConfigureAwait(false);
                await destination.FlushAsync().ConfigureAwait(false);
            }
        }
    }
}
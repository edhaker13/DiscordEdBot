namespace DiscordEdBot
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Audio;
    using Discord.Commands;

    public class MusicService : IDisposable
    {
        private readonly ConcurrentQueue<string> _songQueue = new ConcurrentQueue<string>();
        private IAudioClient _audioClient;
        private string _currentSong;
        private string _currentSongPath;

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

        public async Task<bool> SongInQueue(SocketCommandContext context, string song)
        {
            if (string.Equals(_currentSongPath, song) || string.Equals(_currentSong, song))
            {
                await context.Channel.SendMessageAsync("Song is already playing, ignoring...");
                return true;
            }
            if (!await _songQueue.ToAsyncEnumerable().Any(q => string.Equals(q, song))) return false;
            await context.Channel.SendMessageAsync("Song is already present in the queue, ignoring...");
            return true;
        }

        public async Task<bool> QueueAddAsync(SocketCommandContext context, string song)
        {
            if (await SongInQueue(context, song)) return false;
            _songQueue.Enqueue(song);
            await context.Channel.SendMessageAsync($"Adding song to queue: {song}...");
            return true;
        }

        // TODO: Control playback (info, next, stop, pause?).
        public async Task PlayAsync(SocketCommandContext context, string song = null)
        {
            if (_audioClient == null)
            {
                await JoinChannelAsync(context).ConfigureAwait(false);
                if (_audioClient == null)
                    return;
            }
            if (song == null)
            {
                if (_currentSong != null)
                {
                    await context.Channel.SendMessageAsync("A song is already playing, did you mean to skip?")
                        .ConfigureAwait(false);
                    return;
                }
                if (!_songQueue.TryDequeue(out song))
                {
                    await context.Channel.SendMessageAsync("No items found in the current playlist")
                        .ConfigureAwait(false);
                    return;
                }
            }
            else
            {
                if (await SongInQueue(context, song).ConfigureAwait(false)) return;
            }
            _currentSongPath = song;
            try
            {
                if (File.Exists(song))
                    await PlayFileAsync(context, song).ConfigureAwait(false);
                else
                    await PlayUrlAsync(context, song).ConfigureAwait(false);
                await context.Channel.SendMessageAsync("Finished playing current song!").ConfigureAwait(false);
            }
            finally
            {
                _currentSongPath = null;
            }
            // Try to play next song in the queue
            await PlayAsync(context).ConfigureAwait(false);
        }

        private async Task PlayFileAsync(SocketCommandContext context, string path)
        {
            await QueueAddAsync(context, path).ConfigureAwait(false);
            if (!_songQueue.TryPeek(out path)) return;
            _currentSong = path;
            try
            {
                await context.Channel.SendMessageAsync($"Playing song: '{path}'...").ConfigureAwait(false);
                if (path.EndsWith("opus"))
                    await PlayAsOpusAsync(path);
                else
                    await PlayAsPcmAsync(path, InputToPcm);
            }
            finally
            {
                _songQueue.TryDequeue(out path);
                _currentSong = null;
            }
        }

        private async Task PlayUrlAsync(SocketCommandContext context, string url)
        {
            var process = DecryptUrl(url);
            var decrypted = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var urls = decrypted.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (urls.Length == 0)
            {
                await context.Channel.SendMessageAsync($"Failed to play link: {url}.").ConfigureAwait(false);
                return;
            }
            foreach (var newUrl in urls)
                await QueueAddAsync(context, newUrl).ConfigureAwait(false);
            //await context.Channel.SendMessageAsync($"Playing link as music: {newUrl}").ConfigureAwait(false);
            if (!_songQueue.TryPeek(out url)) return;
            _currentSong = url;
            try
            {
                await PlayAsPcmAsync(url, InputToPcm).ConfigureAwait(false);
            }
            finally
            {
                _songQueue.TryDequeue(out url);
                _currentSong = null;
            }
        }

        private async Task PlayAsOpusAsync(string path)
        {
            // TODO: Wait for DiscordNet to fix opus streams.
            using (var source = File.OpenRead(path))
            using (var destination = _audioClient.CreateOpusStream())
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
            using (var destination = _audioClient.CreatePCMStream(AudioApplication.Music))
            {
                if (process.HasExited || !source.CanRead) return;
                await source.CopyToAsync(destination).ConfigureAwait(false);
                await destination.FlushAsync().ConfigureAwait(false);
            }
        }
    }
}
namespace DiscordEdBot
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Audio;
    using Discord.Commands;

    public class MusicService : IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> _songQueue = new ConcurrentQueue<string>();
        private IAudioClient _audioClient;
        private string _currentSong;
        private string _currentSongPath;
        private CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void Dispose()
        {
            if (_audioClient != null)
            {
                _audioClient.StopAsync().GetAwaiter().GetResult();
                _audioClient.Dispose();
            }
            _cancellationTokenSource?.Dispose();
            while (!_songQueue.IsEmpty) _songQueue.TryDequeue(out _currentSong);
            _audioClient = null;
            _cancellationTokenSource = null;
            _currentSong = null;
            _currentSongPath = null;
        }

        private void ReInitialise()
        {
            _cancellationTokenSource = new CancellationTokenSource();
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

        private async Task JoinChannelAsync(SocketCommandContext context)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null)
            {
                await context.Channel.SendMessageAsync("No valid voice channel to join found.",
                    options: new RequestOptions {CancelToken = CancellationToken});
                return;
            }
            if (_audioClient != null) await _audioClient.StopAsync().ConfigureAwait(false);
            _audioClient = await channel.ConnectAsync().ConfigureAwait(false);
        }

        private async Task<bool> SongInQueue(SocketCommandContext context, string song)
        {
            if (string.Equals(_currentSongPath, song) || string.Equals(_currentSong, song))
            {
                await context.Channel.SendMessageAsync("Song is already playing, ignoring...",
                    options: new RequestOptions {CancelToken = CancellationToken});
                return true;
            }
            if (!await _songQueue.ToAsyncEnumerable().Any(q => string.Equals(q, song), CancellationToken)) return false;
            await context.Channel.SendMessageAsync("Song is already present in the queue, ignoring...",
                options: new RequestOptions {CancelToken = CancellationToken});
            return true;
        }

        public async Task QueuePrintAsync(SocketCommandContext context)
        {
            var songs = _songQueue.ToArray();
            if (songs == null || songs.Length == 0)
            {
                await context.Channel.SendMessageAsync("No songs in the queue.",
                    options: new RequestOptions {CancelToken = CancellationToken}).ConfigureAwait(false);
                return;
            }
            await context.Channel.SendMessageAsync($"Listing the {songs.Length} song(s) enqueued.",
                options: new RequestOptions {CancelToken = CancellationToken}).ConfigureAwait(false);
            for (var i = 0; i < songs.Length; i++)
            {
                await context.Channel.SendMessageAsync($"{i+1}: '{songs[i]}'",
                    options: new RequestOptions {CancelToken = CancellationToken}).ConfigureAwait(false);
            }
        }

        public async Task<bool> QueueAddAsync(SocketCommandContext context, string song)
        {
            if (await SongInQueue(context, song)) return false;
            _songQueue.Enqueue(song);
            await context.Channel.SendMessageAsync($"Adding song to queue: '{song}'...",
                options: new RequestOptions {CancelToken = CancellationToken});
            return true;
        }

        // TODO: Control playback (pause). Pausing requires control over the conversion process.
        public async Task PlayAsync(SocketCommandContext context, string song = null)
        {
            if (CancellationToken.IsCancellationRequested) return;
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

        public async Task SkipAsync(SocketCommandContext context)
        {
             if (_currentSong == null) return;
            _cancellationTokenSource.Cancel();
            try
            {
                await Task.Delay(-1, CancellationToken);
            }
            finally
            {
                ReInitialise();
            }
        }

        public async Task StopAsync(SocketCommandContext context)
        {
            if (_currentSong == null) return;
            _cancellationTokenSource.Cancel();
            try
            {
                await Task.Delay(-1, CancellationToken);
            }
            finally
            {
                Dispose();
                ReInitialise();
            }
        }

        private async Task PlayUrlAsync(SocketCommandContext context, string url)
        {
            if (CancellationToken.IsCancellationRequested) return;
            var process = DecryptUrl(url);
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var urls = output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (urls.Length == 0)
            {
                await context.Channel.SendMessageAsync($"Failed to play link: {url}.",
                    options: new RequestOptions {CancelToken = CancellationToken}).ConfigureAwait(false);
                return;
            }
            foreach (var newUrl in urls)
            {
                // Path is a resolved url, let the queue add it.
                if (newUrl == url && newUrl == _currentSongPath) _currentSongPath = null;
                await QueueAddAsync(context, newUrl).ConfigureAwait(false);
            }
            //await context.Channel.SendMessageAsync($"Playing link as music: {newUrl}").ConfigureAwait(false);
            if (!_songQueue.TryPeek(out url)) return;
            _currentSong = url;
            try
            {
                await PlayWithConversionAsync(url).ConfigureAwait(false);
            }
            finally
            {
                _songQueue.TryDequeue(out url);
                _currentSong = null;
            }
        }

        private async Task PlayWithConversionAsync(string path)
        {
            if (CancellationToken.IsCancellationRequested) return;
            using (var process = InputToOpus(path, application: AudioApplication.Music)) // TODO: Skip conversion if possible
            using (var source = process.StandardOutput.BaseStream)
            using (var destination = _audioClient.CreateOpusStream())
            {
                if (process.HasExited || !source.CanRead) return;
                await source.CopyToAsync(destination, 1024, CancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(CancellationToken).ConfigureAwait(false);
            }
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

        private static Process InputToOpus(string input, int bitrate = 96000, AudioApplication application = AudioApplication.Voice)
        {
            var opusApplication = application == AudioApplication.Voice ? "voip" : application == AudioApplication.Music ? "audio" : "lowdelay";
            var ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel debug -i \"{input}\" -sample_fmt s16 -ar 48000 -ac 2 -acodec libopus -b:a {bitrate} -vbr on -compression_level 0 -frame_duration 20 -application {opusApplication} -map 0:a -f data pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var procces = Process.Start(ffmpeg);
            return procces;
        }
    }
}
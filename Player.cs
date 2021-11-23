using Discord;
using Discord.Audio;
using Discord.Commands;
using System.Diagnostics;
using System.Web;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace FoxyBot
{
    public class Player
    {
        private static Player instance;
        public IAudioClient client;
        public Queue<Song> Queue = new Queue<Song>();
        public IVoiceChannel channel;
        public SocketCommandContext Context;
        public Song currentSong;
        OptionSet options = new OptionSet();






        AudioOutStream discord;
        Process ffmpeg;
        Stream output;

        public static Player getInstance()
        {
            if (instance == null)
            {
                instance = new Player();
            }
            instance.options = OptionSet.LoadConfigFile("YoutubeDLConfig.ini");
            return instance;
        }

        public async Task<string> DownloadFromYoutubeAsync(string urlString)
        {
            //var ytdl = new YoutubeDL();
            //ytdl.YoutubeDLPath = "youtube-dl.exe";
            //ytdl.FFmpegPath = "ffmpeg.exe";
            //ytdl.OutputFolder = "\\YouTube";
            //var cts = new CancellationTokenSource();
            //var res = await ytdl.RunAudioDownload(urlString, AudioConversionFormat., cts.Token, null, null, options);



            var ytdlProc = new YoutubeDLProcess();



            var res1 = await ytdlProc.RunAsync(new[] { urlString }, options);

            var url = new Uri(urlString);
            var videoCode = HttpUtility.ParseQueryString(url.Query).Get("v");
            if (videoCode == null)
            {
                videoCode = url.LocalPath;
            }
            var fileName = "YouTube\\" + videoCode;

            if (File.Exists(fileName))
            {
                return fileName;
            }


            //var url = new Uri(urlString);
            //if (url.Host == "youtu.be" || url.Host == "www.youtube.com")
            //{
            //    var source = @"Cache\\";
            //    var youtube = YouTube.Default;
            //    var vid = youtube.GetVideo(url.ToString());
            //    File.WriteAllBytes(source + vid.FullName, await vid.GetBytesAsync());
            //    var Filename = source + vid.FullName;


            //    return Filename;
            //}

            return null;
        }

        public void PausePlaying()
        {

        }

        public async void StopPlaying()
        {
            if (channel != null && discord != null && ffmpeg != null)
            {
                ffmpeg.Close();
                ffmpeg.Dispose();
                output.Dispose();
                Queue.Clear();

                await channel.DisconnectAsync();
                await discord.FlushAsync();

                File.Delete(currentSong.Path);
            }
        }

        public string GetQueueList()
        {
            return currentSong + Environment.NewLine + String.Join(Environment.NewLine, Queue.Select(x => x.Link));
        }


        public void AddToQueue(string link, IVoiceChannel channel, SocketCommandContext Context)
        {
            var song = new Song(link, DownloadFromYoutubeAsync(link).Result);

            if (this.channel == null)
            {
                Queue.Enqueue(song);
                this.channel = channel;
                this.Context = Context;
                this.Play();
            }
            else if (this.channel.Id != channel.Id)
            {
                _ = Context.Channel.SendMessageAsync("Бот уже находится в другом канале");
            }
            else
            {
                Queue.Enqueue(song);
            }
        }

        public async void Play()
        {
            this.client = await channel.ConnectAsync();
            client.Disconnected += AudioClient_Disconnected;

            while (true)
            {
                if (!Queue.TryDequeue(out currentSong))
                {
                    break;
                }
                await SendAsync(currentSong);
                File.Delete(currentSong.Path);

            }
            _ = Context.Channel.SendMessageAsync("Очередь пуста(");
            await channel.DisconnectAsync();
        }


        private async Task AudioClient_Disconnected(Exception arg)
        {
            _ = Task.Factory.StartNew(() => { 
                channel = null; 
                client.Dispose();
                File.Delete(currentSong.Path);
            });
        }


        private async Task SendAsync(Song song)
        {
            // Create FFmpeg using the previous example
            using (ffmpeg = CreateStream(song.Path))
            using (output = ffmpeg.StandardOutput.BaseStream)
            using (discord = client.CreatePCMStream(AudioApplication.Mixed))
            {
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }


        private Process CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1 -b:a 96k -bufsize 1M",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }
    }
}

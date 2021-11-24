using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace FoxyBot.Modules
{
    public class MusicModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;

        public MusicModule(LavaNode lavaNode)
        {
            _lavaNode = lavaNode;
        }


        [Command("Play", RunMode = RunMode.Async)]
        public async Task PlayAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsync("Непонятный запрос");
                return;
            }



            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                if (_lavaNode.HasPlayer(Context.Guild))
                {
                    await ReplyAsync("Бот уже находится в голосовом канале");
                    return;
                }

                var voiceState = Context.User as IVoiceState;
                if (voiceState?.VoiceChannel == null)
                {
                    await ReplyAsync("Необходимо находиться в голосовом канале!");
                    return;
                }

                try
                {
                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                    //await ReplyAsync($"Joined {voiceState.VoiceChannel.Name}!");
                }
                catch (Exception exception)
                {
                    await ReplyAsync(exception.Message);
                }
            }

            if (Uri.IsWellFormedUriString(query, UriKind.RelativeOrAbsolute))
            {
                var uri = new Uri(query);

                //var vidId = HttpUtility.ParseQueryString(uri.Query).Get("v");
                //if (vidId == null)
                //{
                //    vidId = uri.LocalPath.TrimEnd('/');
                //}
            }


            var searchResponse = await _lavaNode.SearchYouTubeAsync(query);
            //var x = searchResponse.Tracks.FirstOrDefault();
            

            if (searchResponse.Status == Victoria.Responses.Search.SearchStatus.LoadFailed ||
                searchResponse.Status == Victoria.Responses.Search.SearchStatus.NoMatches)
            {
                await ReplyAsync($"Ничего не найдено по запросу `{query}`.");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);

            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                {
                    foreach (var track in searchResponse.Tracks)
                    {
                        player.Queue.Enqueue(track);
                    }

                    await ReplyAsync($"В очередь добавлено {searchResponse.Tracks.Count} треков");
                }
                else
                {
                    var track = searchResponse.Tracks.First();
                    player.Queue.Enqueue(track);
                    await ReplyAsync($"Добавлено в очередь: **{track.Title}**");
                }
            }
            else
            {
                var track = searchResponse.Tracks.First();

                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                {
                    for (var i = 0; i < searchResponse.Tracks.Count; i++)
                    {
                        if (i == 0)
                        {
                            await player.PlayAsync(track);
                            await ReplyAsync($"Сейчас играет: {track.Title}");
                        }
                        else
                        {
                            player.Queue.Enqueue(searchResponse.Tracks.ElementAt(i));
                        }
                    }

                    await ReplyAsync($"В очередь добавлено {searchResponse.Tracks.Count} треков");
                }
                else
                {
                    await player.PlayAsync(track);
                    await ReplyAsync($"Сейчас играет: **{track.Title}**");
                }
            }

        }

        [Command("Stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsync("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsync("Бот уже не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsync("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.PlayerState == PlayerState.Paused || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync($"Музыка и так не играет");
                return;
            }

            await player.StopAsync();

            await ReplyAsync($"Остановлено. Очередь треков ");

        }

        [Command("Pause", RunMode = RunMode.Async)]
        public async Task PauseAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsync("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsync("Бот уже не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsync("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.PlayerState == PlayerState.Paused || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync($"Музыка и так не играет");
                return;
            }

            await player.PauseAsync();
            await ReplyAsync($"Ставим паузу... Время сходить за печеньками?");
        }

        [Command("Resume", RunMode = RunMode.Async)]
        public async Task ResumeAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsync("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsync("Бот уже не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsync("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.PlayerState == PlayerState.Playing)
            {
                await ReplyAsync($"Музыка и так играет");
                return;
            }

            await player.ResumeAsync();
            await ReplyAsync($"Продолжаем воспроизведение");
        }

        [Command("Skip", RunMode = RunMode.Async)]
        public async Task SkipAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsync("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsync("Бот уже не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel) {
                await ReplyAsync("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.Queue.Count == 0)
            {
                await ReplyAsync("Очередь треков пустая");
                return;
            }

            await player.SkipAsync();
            await ReplyAsync($"Пропускаем трек... Сейчас играет **{player.Track.Title}**");

        }

        [Command("q", RunMode = RunMode.Async)]
        public async Task GetQueueAsync()
        {
            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player != null)
            {
                if (player.Queue.Count != 0)
                {
                    var queue = "Будущие треки:" + Environment.NewLine + String.Join(Environment.NewLine, player.Queue.Select(x => x.Title));
                    await ReplyAsync(queue);
                }
            }
            

        }

    }
}

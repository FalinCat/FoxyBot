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

        [Command("Search", RunMode = RunMode.Async)]
        public async Task SearchAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("Непонятный запрос");
                return;
            }
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                if (_lavaNode.HasPlayer(Context.Guild))
                {
                    await ReplyAsyncWithCheck("Бот уже находится в голосовом канале");
                    return;
                }

                var voiceState = Context.User as IVoiceState;
                if (voiceState?.VoiceChannel == null)
                {
                    await ReplyAsyncWithCheck("Необходимо находиться в голосовом канале!");
                    return;
                }

                try
                {
                    //await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                    //await ReplyAsync($"Joined {voiceState.VoiceChannel.Name}!");
                }
                catch (Exception exception)
                {
                    await ReplyAsync(exception.Message);
                }
            }

            if (query.Contains("https://") || query.Contains("http://"))
            {
                await ReplyAsyncWithCheck($"Не надо искать ссылки");
                return;
            }

            var searchResponse = await _lavaNode.SearchYouTubeAsync(query);
            if (searchResponse.Status == Victoria.Responses.Search.SearchStatus.LoadFailed ||
                searchResponse.Status == Victoria.Responses.Search.SearchStatus.NoMatches)
            {
                await ReplyAsyncWithCheck($"Ничего не найдено по запросу `{query}`.");
                return;
            }

            //var answer = "Вот что я нашел:" + Environment.NewLine + String.Join(Environment.NewLine, searchResponse.Tracks.Select(t => t.Title).ToList());

            var str = new StringBuilder();
            str.AppendLine("Вот что я нашел:");

            for (int i = 0; i < searchResponse.Tracks.Count; i++)
            {
                str.AppendLine($"{i} - {searchResponse.Tracks.ElementAt(i).Title} [{new DateTime(searchResponse.Tracks.ElementAt(i).Duration.Ticks):HH:mm:ss}]");
            }

            await ReplyAsyncWithCheck(str.ToString());
        }


        [Command("Play", RunMode = RunMode.Async)]
        public async Task PlayAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("Непонятный запрос");
                return;
            }



            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                if (_lavaNode.HasPlayer(Context.Guild))
                {
                    await ReplyAsyncWithCheck("Бот уже находится в голосовом канале");
                    return;
                }

                var voiceState = Context.User as IVoiceState;
                if (voiceState?.VoiceChannel == null)
                {
                    await ReplyAsyncWithCheck("Необходимо находиться в голосовом канале!");
                    return;
                }

                try
                {
                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                    //await ReplyAsync($"Joined {voiceState.VoiceChannel.Name}!");
                }
                catch (Exception exception)
                {
                    await ReplyAsyncWithCheck(exception.Message);
                }
            }


            int number = -1;
            if (int.TryParse(query, out number))
            {
                var messages = Context.Channel.GetCachedMessages(3);

                string searchString = "";

                //messages.FirstOrDefault(message => message.Content.ToLower().Contains(searchString));

                foreach (var message in messages)
                {
                    if (message.Content.ToLower().Contains("$search"))
                    {
                        searchString = message.Content.TrimStart("$search".ToCharArray());
                        break;
                    }
                }

                var sResponce = await _lavaNode.SearchYouTubeAsync(searchString);
                var track = sResponce.Tracks.ElementAtOrDefault(number);
                if (track != null)
                {
                    var plr = _lavaNode.GetPlayer(Context.Guild);

                    if (plr.PlayerState == PlayerState.Playing || plr.PlayerState == PlayerState.Paused)
                    {
                        plr.Queue.Enqueue(track);
                        await ReplyAsyncWithCheck($"Добавлено в очередь: **{track.Title}**");
                    }
                    else
                    {
                        await plr.PlayAsync(track);
                        await ReplyAsyncWithCheck($"Сейчас играет: **{track.Title}**");
                    }
                    return;
                }
                else
                {
                    await ReplyAsyncWithCheck($"По неведомой причине найти трек не удалось");
                }

                return;
            }



            //query = query.Split('\n')[0];
            if (query.Contains("youtu.be") || query.Contains("youtube.com"))
            {
                query = query.Split(' ')[0];
            }


            var searchResponse = await _lavaNode.SearchYouTubeAsync(query);
            if (searchResponse.Status == Victoria.Responses.Search.SearchStatus.LoadFailed ||
                searchResponse.Status == Victoria.Responses.Search.SearchStatus.NoMatches)
            {
                await ReplyAsyncWithCheck($"Ничего не найдено по запросу `{query}`.");
                return;
            }

            LavaTrack foundedTrack = searchResponse.Tracks.First();
            if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
            {
                var uri = new Uri(query);
                var vidId = HttpUtility.ParseQueryString(uri.Query).Get("v");

                if (vidId == null)
                {
                    vidId = uri.LocalPath.Trim('/');
                }

                foundedTrack = searchResponse.Tracks.FirstOrDefault(x => x.Id == vidId);
                if (foundedTrack == null)
                {
                    await ReplyAsyncWithCheck($"При поиске трека произошел фэйл");
                }
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

                    await ReplyAsyncWithCheck($"В очередь добавлено {searchResponse.Tracks.Count} треков");
                }
                else
                {
                    //var track = searchResponse.Tracks.First();
                    player.Queue.Enqueue(foundedTrack);
                    await ReplyAsyncWithCheck($"Добавлено в очередь: **{foundedTrack.Title}**");
                }
            }
            else
            {
                //var track = searchResponse.Tracks.First();

                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                {
                    for (var i = 0; i < searchResponse.Tracks.Count; i++)
                    {
                        if (i == 0)
                        {
                            await player.PlayAsync(foundedTrack);
                            await ReplyAsyncWithCheck($"Сейчас играет: {foundedTrack.Title}");
                        }
                        else
                        {
                            player.Queue.Enqueue(searchResponse.Tracks.ElementAt(i));
                        }
                    }

                    await ReplyAsyncWithCheck($"В очередь добавлено {searchResponse.Tracks.Count} треков");
                }
                else
                {
                    await player.PlayAsync(foundedTrack);
                    await ReplyAsyncWithCheck($"Сейчас играет: **{foundedTrack.Title}**");
                }
            }

        }

        [Command("Stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsyncWithCheck("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsyncWithCheck("Бот уже не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsyncWithCheck("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.PlayerState == PlayerState.Paused || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsyncWithCheck($"Музыка и так не играет");
                return;
            }

            await player.StopAsync();

            //await ReplyAsync($"Остановлено");

        }

        [Command("Pause", RunMode = RunMode.Async)]
        public async Task PauseAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsyncWithCheck("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsyncWithCheck("Бот уже не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsyncWithCheck("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.PlayerState == PlayerState.Paused || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsyncWithCheck($"Музыка и так не играет");
                return;
            }

            await player.PauseAsync();
            await ReplyAsyncWithCheck($"Ставим паузу... Время сходить за печеньками?");
        }

        [Command("Resume", RunMode = RunMode.Async)]
        public async Task ResumeAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsyncWithCheck("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsyncWithCheck("Бот уже не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsyncWithCheck("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.PlayerState == PlayerState.Playing)
            {
                await ReplyAsyncWithCheck($"Музыка и так играет");
                return;
            }

            await player.ResumeAsync();
            await ReplyAsyncWithCheck($"Продолжаем воспроизведение");
        }

        [Command("Skip", RunMode = RunMode.Async)]
        public async Task SkipAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsyncWithCheck("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsyncWithCheck("Бот уже не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsyncWithCheck("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.Queue.Count == 0)
            {
                //await ReplyAsync("Очередь треков пустая");
                await player.StopAsync();
                return;
            }

            await player.SkipAsync();
            await ReplyAsyncWithCheck($"Пропускаем трек... Сейчас играет **{player.Track.Title}**");

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
                    await ReplyAsyncWithCheck(queue);
                }
            }


        }

        private async Task ReplyAsyncWithCheck(string message)
        {
            const ulong vladId = 330647539076300801;
            const ulong oxyId = 450741374132682762;
            const ulong falinId = 444168548055777310;
            const ulong ozmaId = 390928341848293376;
            const ulong juibId = 391341859466772480;
            const ulong meddoId = 126401338945961985;
            const ulong trimarId = 268034213838716929;
            const ulong badfraggId = 913053197239726140;
            const ulong duhotaId = 303207860576321536;
            const ulong kidneyId = 303947320905695233;
            const ulong falcaId = 638834185167175683;

            switch (Context.User.Id)
            {
                case vladId:
                    message = "Кста, " + message;
                    await ReplyAsync(message);
                    return;
                case oxyId:
                    message = "Пипец на холодец! " + message;
                    await ReplyAsync(message);
                    return;
                case ozmaId:
                    message = "Трында! " + message;
                    await ReplyAsync(message);
                    return;
                case juibId:
                    message = "Леонид Кагутин, " + message;
                    await ReplyAsync(message);
                    return;
                case meddoId:
                    message = "Чё началось-то? " + message;
                    await ReplyAsync(message);
                    return;
                case trimarId:
                    message = "30 золотых монет " + message;
                    await ReplyAsync(message);
                    return;
                case badfraggId:
                    message = "Отдай, " + message;
                    await ReplyAsync(message);
                    return;
                case falcaId:
                    message = "Ништяяяк... " + message;
                    await ReplyAsync(message);
                    return;
                case kidneyId:
                    message = "Рандом подкручен, признавайся! " + message;
                    await ReplyAsync(message);
                    return;
                case falinId:
                    message = "Мой повелитель, " + message;
                    await ReplyAsync(message);
                    return;
                case duhotaId:
                    message = "Как то душно стало, " + message;
                    await ReplyAsync(message);
                    return;
                default:
                    break;
            }
            await ReplyAsync(message);
        }

    }


}

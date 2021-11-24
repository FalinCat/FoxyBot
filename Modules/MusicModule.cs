﻿using Discord;
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

        [Command("help", RunMode = RunMode.Async)]
        public async Task SearchAsyncCut()
        {
            await ReplyAsyncWithCheck(@"
play - p - поиск на ютубе
pause - пауза
resume - продолжить
stop - остановить
skip - пропустить
search - s - поиск. После получения списка писать команду $play N где N - номер трека из списка (иногда ютуб решает поменять местами треки в результате и надо еще раз сделать $search)
np - что сейчас играет
");
        }

        [Command("np", RunMode = RunMode.Async)]
        public async Task NowPlayingAsync()
        {
            var player = _lavaNode.GetPlayer(Context.Guild);

            var str = new StringBuilder();
            str.AppendLine(player.Track.Title);
            str.AppendLine("Текущая позиция:" + new DateTime(player.Track.Position.Ticks).ToString("HH:mm:ss"));
            await ReplyAsyncWithCheck(str.ToString());
        }

        [Command("s", RunMode = RunMode.Async)]
        public async Task SearchAsyncCut([Remainder] string query)
        {
            await SearchAsync(query);
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

        [Command("P", RunMode = RunMode.Async)]
        public async Task PlayAsyncCut([Remainder] string query)
        {
            await PlayAsync(query);
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
            if ((searchResponse.Status == Victoria.Responses.Search.SearchStatus.LoadFailed ||
                searchResponse.Status == Victoria.Responses.Search.SearchStatus.NoMatches) &&
                !Uri.IsWellFormedUriString(query, UriKind.Absolute))
            {
                await ReplyAsyncWithCheck($"Ничего не найдено по запросу `{query}`.");
                return;
            }

            LavaTrack? foundedTrack = searchResponse.Tracks.FirstOrDefault();
            if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
            {

                var uri = new Uri(query);
                var vidId = HttpUtility.ParseQueryString(uri.Query).Get("v");

                if (vidId == null)
                {
                    vidId = uri.LocalPath.Trim('/').Split('?')[0];
                }

                searchResponse = await _lavaNode.SearchYouTubeAsync(vidId);
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
                    await ReplyAsyncWithCheck($"Добавлено в очередь: **{foundedTrack?.Title}**");
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
                            await ReplyAsyncWithCheck($"Сейчас играет: {foundedTrack?.Title}");
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
                    await ReplyAsyncWithCheck($"Сейчас играет: **{foundedTrack?.Title}**");
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
                    var str = new StringBuilder();
                    str.AppendLine("Треки в очереди:");
                    for (int i = 0; i < player.Queue.Count; i++)
                    {
                        str.AppendLine($"{i} - {player.Queue.ElementAt(i).Title} [{new DateTime(player.Queue.ElementAt(i).Duration.Ticks):HH:mm:ss}]");
                    }
                    //var totalTime = player.Queue.Sum(x => x.Duration.Ticks);
                    var totalTime = player.Queue.Aggregate
                (TimeSpan.Zero,
                (sumSoFar, nextMyObject) => sumSoFar + nextMyObject.Duration);

                    str.AppendLine("Всего времени плейлиста: " + new DateTime(totalTime.Ticks).ToString("HH:mm:ss"));

                    //var queue = "Будущие треки:" + Environment.NewLine + String.Join(Environment.NewLine, player.Queue.Select(x => x.Title));
                    await ReplyAsyncWithCheck(str.ToString());
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
            const ulong kidneyId = 303947320905695233;
            const ulong falcaId = 638834185167175683;
            const ulong jutaId = 499651025494474752;
            const ulong elengelId = 378243330573598760;


            var jokesList = new List<string>();
            var random = new Random();
            switch (Context.User.Id)
            {
                case vladId:
                    jokesList.Add("Кста, ");
                    jokesList.Add("Владдудос, ");
                    jokesList.Add("Где третий ГС? ");
                    jokesList.Add("Продам пейран, кста! ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case oxyId:
                    jokesList.Add("Пипец на холодец! ");
                    jokesList.Add("Мяяяя.... ");
                    jokesList.Add("Я надеюсь ты сейчас в шоколадном бубличке? ");
                    jokesList.Add("Простите, ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case ozmaId:
                    jokesList.Add("Трында! ");
                    jokesList.Add("Фыр-фыр-фыр... ");
                    jokesList.Add("Ваше Лисичество, ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case juibId:
                    jokesList.Add("Леонид Кагутин, ");
                    jokesList.Add("ММ лагает? ");
                    jokesList.Add("Леонид Кагутин, продажи уже просчитались? ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case meddoId:
                    jokesList.Add("Чё началось-то? ");
                    jokesList.Add("Бот застрял. ");
                    jokesList.Add("Оно поломалось... ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case trimarId:
                    jokesList.Add("За Гомеза! ");
                    jokesList.Add("Вот мои 30 золотых монет. ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case badfraggId:
                    jokesList.Add("Отдай, ");
                    jokesList.Add("Ваше преступление фотофиксируется. ");
                    jokesList.Add("Вас ждут в Жабском суде! ");
                    jokesList.Add("Вас ждут в Гаагском суде! ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case falcaId:
                    jokesList.Add("Ништяяяк... ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case kidneyId:
                    jokesList.Add("Рандом подкручен, признавайся! ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case falinId:
                    jokesList.Add("Мой создатель, ");
                    jokesList.Add("Мой повелитель, ");
                    jokesList.Add("Милорд, ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case jutaId:
                    jokesList.Add("Ели мясо оборотнИ, пивом запивали! ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case elengelId:
                    jokesList.Add("Шестизначный дипс, кста. ");
                    jokesList.Add("Батла неоптимизирована =) ");
                    jokesList.Add("Это уже какая бутылочка коньяка? ");
                    jokesList.Add("Го винишка? ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                default:
                    await ReplyAsync(message);
                    break;
            }

        }
    }


}

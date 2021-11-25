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
q - посмотреть очередь
np - что сейчас играет
");
        }

        [Command("np", RunMode = RunMode.Async)]
        public async Task NowPlayingAsync()
        {
            var player = _lavaNode.GetPlayer(Context.Guild);

            var str = new StringBuilder();
            str.Append($"{player.Track.Title} <{player.Track.Url}>");
            str.AppendLine($" - [{new DateTime(player.Track.Position.Ticks).ToString("HH:mm:ss")}] " +
                $"/[{new DateTime(player.Track.Duration.Ticks).ToString("HH:mm:ss")}]");
            //str.AppendLine(player.Track.Url);
            await ReplyAsyncWithCheck(str.ToString());



            //await ReplyAsync("", false, );
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
            //var search = await _lavaNode.SearchAsync(Victoria.Responses.Search.SearchType.Direct, query);

            var origQuery = query;
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



            var player = _lavaNode.GetPlayer(Context.Guild);
            int number = -1;
            if (int.TryParse(query, out number))
            {
                var messages = Context.Channel.GetCachedMessages(10);

                foreach (var message in messages)
                {
                    if (message.Content.ToLower().Contains("$search"))
                    {
                        query = message.Content.TrimStart("$search".ToCharArray());
                        break;
                    }
                }
            }
            else
            {
                number = -1;
            }

            string? vidId = null;
            if (query.Contains("youtu.be") || query.Contains("youtube.com"))
            {
                query = query.Split(' ')[0].Replace("https://music.", "https://").Replace("&feature=share", "");
                var uri = new Uri(query);
                vidId = HttpUtility.ParseQueryString(uri.Query).Get("v");

                if (vidId == null)
                {
                    vidId = uri.LocalPath.Trim('/').Split('?')[0];
                }
            }

            //https://youtu.be/rk6zxV0Z4Xk?list=PLlvCpedNZ4rjUjeZpyJF5i0Yn4EP9589T
            //https://www.youtube.com/watch?v=rk6zxV0Z4Xk&list=PLlvCpedNZ4rjUjeZpyJF5i0Yn4EP9589T &index=3

            Victoria.Responses.Search.SearchResponse searchResponse;
            int index = -1;
            if (query.Contains("list="))
            {
                var uri = new Uri(origQuery);
                int.TryParse(HttpUtility.ParseQueryString(uri.Query).Get("index"), out index);
                var list = HttpUtility.ParseQueryString(uri.Query).Get("list");
                var v = HttpUtility.ParseQueryString(uri.Query).Get("v");

                var str = "https://youtu.be/" + v + "?list=" + list + "&index=" + index;


                searchResponse = await _lavaNode.SearchAsync(Victoria.Responses.Search.SearchType.Direct, str);
            }
            else if (query.Contains("https://music.youtube.com"))
            {
                searchResponse = await _lavaNode.SearchAsync(Victoria.Responses.Search.SearchType.Direct, origQuery);
            }
            else
            {
                searchResponse = await _lavaNode.SearchYouTubeAsync(query);
            }

            LavaTrack? track = null;
            if ((searchResponse.Status == Victoria.Responses.Search.SearchStatus.LoadFailed ||
                searchResponse.Status == Victoria.Responses.Search.SearchStatus.NoMatches))
            {
                searchResponse = await _lavaNode.SearchYouTubeAsync(vidId);
                if ((searchResponse.Status == Victoria.Responses.Search.SearchStatus.LoadFailed ||
                    searchResponse.Status == Victoria.Responses.Search.SearchStatus.NoMatches))
                {
                    await ReplyAsyncWithCheck($"Ничего не найдено по запросу `{query}`.");
                    return;
                }
            }

            if (number != -1)
            {
                track = searchResponse.Tracks.ElementAtOrDefault(number);
            }
            else if (vidId != null)
            {
                track = searchResponse.Tracks.FirstOrDefault(x => x.Id == vidId);
                if (track == null && !origQuery.Contains("list="))
                {
                    await ReplyAsyncWithCheck($"При поиске трека произошел фэйл. Бот не нашел именно трек по ссылке :(");
                    return;
                }
            }
            else
            {
                track = searchResponse.Tracks.FirstOrDefault();
                if (track == null)
                {
                    await ReplyAsyncWithCheck($"При поиске трека произошел фэйл");
                }
            }




            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                {
                    for (var i = searchResponse.Playlist.SelectedTrack; i < searchResponse.Tracks.Count; i++)
                    {
                        player.Queue.Enqueue(searchResponse.Tracks.ElementAt(i - 1));
                    }

                    await ReplyAsyncWithCheck($"В очередь добавлено {searchResponse.Tracks.Count} треков");
                }
                else
                {
                    player.Queue.Enqueue(track);
                    await ReplyAsyncWithCheck($"Добавлено в очередь: **{track?.Title}**");
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
                {
                    int count = 0;
                    for (var i = searchResponse.Playlist.SelectedTrack; i < searchResponse.Tracks.Count; i++)
                    {
                        var t = searchResponse.Tracks.ElementAt(i);
                        if (i == searchResponse.Playlist.SelectedTrack)
                        {
                            await player.PlayAsync(t);
                            await ReplyAsyncWithCheck($"Сейчас играет: {t.Title} - <{t.Url}>");
                        }
                        else
                        {
                            player.Queue.Enqueue(t);
                        }
                        count++;
                    }

                    await ReplyAsyncWithCheck($"В очередь добавлено {count} треков");
                }
                else
                {
                    await player.PlayAsync(track);
                    await ReplyAsyncWithCheck($"Сейчас играет: **{player.Track.Title}**");
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
            //var player = _lavaNode.GetPlayer(Context.Guild);
            //var player1 = ;
            if (_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer? player))
            {
                if (player.Track != null || player.Queue.Count != 0)
                {
                    var str = new StringBuilder();
                    if (player.Track != null)
                    {
                        str.AppendLine($"Сейчас играет: {player.Track.Title} Осталось [{new DateTime((player.Track.Duration - player.Track.Position).Ticks):HH:mm:ss}] " +
                        $"<{player.Track.Url}>");
                    }


                    for (int i = 0; i < player.Queue.Count; i++)
                    {
                        str.AppendLine($"{i} - {player.Queue.ElementAt(i).Title} [{new DateTime(player.Queue.ElementAt(i).Duration.Ticks):HH:mm:ss}] " +
                            $"<{ player.Queue.ElementAt(i).Url}>");

                        if (i >= 10)
                        {
                            str.AppendLine("Там есть еще треки, но они не влезли в сообщение");
                            break;
                        }
                    }
                    //var totalTime = player.Queue.Sum(x => x.Duration.Ticks);

                    var q = new List<TimeSpan>();
                    q.AddRange(player.Queue.Select(x => x.Duration).ToList());
                    q.Add(player.Track.Duration - player.Track.Position);

                    var totalTime = q.Aggregate
                                    (TimeSpan.Zero,
                                    (sumSoFar, nextMyObject) => sumSoFar + nextMyObject);


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
            const ulong badfraggId = 383160191899795457;
            const ulong kidneyId = 303947320905695233;
            const ulong falcaId = 638834185167175683;
            const ulong jutaId = 499651025494474752;
            const ulong elengelId = 378243330573598760;
            const ulong sovaId = 169892911301787648;
            const ulong elizabethId = 256798937095208960;
            const ulong minorisId = 377851183479390208;


            var jokesList = new List<string>();
            var random = new Random();
            switch (Context.User.Id)
            {
                case vladId:
                    jokesList.Add("Кста, ");
                    jokesList.Add("Владдудос, ");
                    jokesList.Add("Где третий ГС? ");
                    jokesList.Add("Продам пейран, кста! ");
                    jokesList.Add("Найс демедж,найс баланс! ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case oxyId:
                    jokesList.Add("Пипец на холодец! ");
                    jokesList.Add("Мяяяя.... ");
                    jokesList.Add("Я надеюсь ты сейчас в шоколадном бубличке? ");
                    jokesList.Add("Простите, ");
                    jokesList.Add("Вивинг эвэрэйдж, ");
                    jokesList.Add("Жаренные булочки? ");
                    if (DateTime.Now.Hour > 20)
                    {
                        jokesList.Add("Не ем после шести!!! ");
                    }
                    if (DateTime.Now.Hour > 22)
                    {
                        jokesList.Add("Окси, иди спать! ");
                    }
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case ozmaId:
                    jokesList.Add("Трында! ");
                    jokesList.Add("Фыр-фыр-фыр... ");
                    jokesList.Add("Ваше Лисичество, ");
                    jokesList.Add("Опять спекаться в хила? ");
                    jokesList.Add("Опять спекаться с хила? ");
                    jokesList.Add("Погоди, сейчас переспекаюсь в хила... ");
                    if (DateTime.Now.Hour < 9)
                    {
                        jokesList.Add("Так рано не спишь, все окей? ");
                    }
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case juibId:
                    jokesList.Add("Леонид Кагутин, ");
                    jokesList.Add("ММ сегодня не лагает? ");
                    jokesList.Add("Леонид Кагутин, продажи уже просчитались? ");
                    jokesList.Add("Погоди, у меня место в сумке закончилось... ");
                    jokesList.Add("Погоди, у меня место в очереди закончилось... ");
                    jokesList.Add("Релеквин не трогай! (оба) ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case meddoId:
                    jokesList.Add("Чё началось-то? ");
                    jokesList.Add("Опять застрял? ");
                    jokesList.Add("Оно поломалось... ");
                    jokesList.Add("Я ничего не трогал! ");
                    jokesList.Add("Ты ничего не трогал? ");
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
                    jokesList.Add("35/36 ");
                    jokesList.Add("Сегодня будем бомбить? ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case falinId:
                    jokesList.Add("Мой создатель, ");
                    jokesList.Add("Мой повелитель, ");
                    jokesList.Add("Милорд, ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case jutaId:
                    jokesList.Add("Ели мясо оборотнИ, амброзией запивали! ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case elengelId:
                    jokesList.Add("Шестизначный дипс, кста. ");
                    jokesList.Add("Батла неоптимизирована =) ");
                    jokesList.Add("Это уже какая бутылочка коньяка? ");
                    jokesList.Add("Го винишка? ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case sovaId:
                    jokesList.Add("Совень, забери!!! ");
                    jokesList.Add("Пора менять сим-карту? ");
                    jokesList.Add("Пора дипсить! ");
                    jokesList.Add("Пора переходить на 3g ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case elizabethId:
                    jokesList.Add("Если есть в кармане пачка... Ой, простите ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                case minorisId:
                    jokesList.Add("Пора править график? ");
                    jokesList.Add("Ту-ту-ру ");
                    await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
                    break;
                default:
                    await ReplyAsync(message);
                    break;
            }

        }
    }


}

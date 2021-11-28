﻿using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Web;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace FoxyBot.Modules
{
    public class MusicModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        ILogger<DiscordClientService> _logger;


        public MusicModule(LavaNode lavaNode, ILogger<DiscordClientService> logger)
        {
            _lavaNode = lavaNode;
            _logger = logger;
            //_lavaNode.OnTrackStarted += _lavaNode_OnTrackStarted;
            //_lavaNode.OnTrackEnded += _lavaNode_OnTrackEnded;
            //_lavaNode.OnTrackStarted += _lavaNode_OnTrackStarted;

        }

        [Command("help", RunMode = RunMode.Async)]
        public async Task SearchAsyncCut()
        {
            await ReplyAsyncWithCheck(@"
play - p - поиск на ютубе
playnext - pn - поставить трек следующим в очереди после сейчас проигрываемого
pause - пауза
resume - продолжить
stop - остановить
skip - пропустить
remove - удалить трек из очереди (номер трека можно посмотреть командой $q)
seek - перемотать на время. Например 1:17 - 1 минута, 17 секунд. Или 4:19:27 - 4 часа, 19 минут, 27 секунд
clear - очистить очередь не трогая текущий трек
shuffle - перемешать очередь
search - s - поиск. После получения списка писать команду $play N где N - номер трека из списка (иногда ютуб решает поменять местами треки в результате и надо еще раз сделать $search)
q - посмотреть очередь
np - что сейчас играет
kick - пнуть бота нафиг из канала, также пнуть если он завис
");
        }

        [Command("Clear", RunMode = RunMode.Async)]
        private async Task ClearAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                if (voiceState?.VoiceChannel == null)
                {
                    await ReplyAsyncWithCheck("Необходимо находиться в голосовом канале!");
                    return;
                }
                if (_lavaNode.TryGetPlayer(Context.Guild, out var botChannel) && (botChannel.VoiceChannel.Id != voiceState?.VoiceChannel.Id))
                {
                    await ReplyAsyncWithCheck("Бот уже находится в голосовом канале: " + _lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name +
                        ", а вы в канале - " + voiceState?.VoiceChannel);
                    return;
                }
            }

            var player = _lavaNode?.GetPlayer(Context.Guild);
            if (player != null)
            {
                player.Queue.Clear();
                _logger.LogDebug("Очередь очищенна");
                await ReplyAsyncWithCheck("Очередь очищенна");
            }

        }

        [Command("Seek", RunMode = RunMode.Async)]
        private async Task SeekAsync([Remainder] string query)
        {


            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("Непонятный запрос");
                return;
            }

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            var voiceState = Context.User as IVoiceState;
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                if (voiceState?.VoiceChannel == null)
                {
                    await ReplyAsyncWithCheck("Необходимо находиться в голосовом канале!");
                    return;
                }
                if (_lavaNode.TryGetPlayer(Context.Guild, out var botChannel) && (botChannel.VoiceChannel.Id != voiceState?.VoiceChannel.Id))
                {
                    await ReplyAsyncWithCheck("Бот уже находится в голосовом канале: " + player.VoiceChannel.Name +
                        ", а вы в канале - " + voiceState?.VoiceChannel);
                    return;
                }
            }

            var times = query.Split(':');
            int hours = 0, minutes = 0, sec = 0;
            switch (times.Length)
            {
                case 3:
                    if (!int.TryParse(times[0], out hours) &
                        !int.TryParse(times[1], out minutes) &
                        !int.TryParse(times[2], out sec))
                    {
                        await ReplyAsyncWithCheck("Бот не понимает на какой момент надо перемотать :(");
                    }

                    break;
                case 2:
                    if (!int.TryParse(times[0], out minutes) &
                        !int.TryParse(times[1], out sec))
                        await ReplyAsyncWithCheck("Бот не понимает на какой момент надо перемотать :(");

                    break;
                case 1:
                    if (!int.TryParse(times[0], out sec))
                        await ReplyAsyncWithCheck("Бот не понимает на какой момент надо перемотать :(");
                    break;
                default:
                    break;
            }
            if (minutes > 59 || sec > 59)
            {
                await ReplyAsync("Какой то странный формат времени... Ты часом не Меддо?");
                return;
            }
            var ts = new TimeSpan(hours, minutes, sec);
            await player.SeekAsync(ts);
            await ReplyAsyncWithCheck($"Переметал на {query}");
        }

        [Command("pn", RunMode = RunMode.Async)]
        private async Task PlayNextAsyncShort([Remainder] string query)
        {
            _ = PlayNextAsync(query);
        }

        [Command("Playnext", RunMode = RunMode.Async)]
        private async Task PlayNextAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("Непонятный запрос");
                return;
            }

            var voiceState = Context.User as IVoiceState;
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                if (voiceState?.VoiceChannel == null)
                {
                    await ReplyAsyncWithCheck("Необходимо находиться в голосовом канале!");
                    return;
                }
                if (_lavaNode.TryGetPlayer(Context.Guild, out var botChannel) && (botChannel.VoiceChannel.Id != voiceState?.VoiceChannel.Id))
                {
                    await ReplyAsyncWithCheck("Бот уже находится в голосовом канале: " + _lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name +
                        ", а вы в канале - " + voiceState?.VoiceChannel);
                    return;
                }

                try
                {
                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                }
                catch (Exception exception)
                {
                    await ReplyAsyncWithCheck(exception.Message);
                    return;
                }
            }


            var result = SearchTrack(query).Result;
            await PlayMusicAsync(result, true);

        }


        [Command("p", RunMode = RunMode.Async)]
        public async Task TestShortAsync([Remainder] string query)
        {
            await PlayAsync(query);
        }

        [Command("Play", RunMode = RunMode.Async)]
        private async Task PlayAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("Непонятный запрос");
                return;
            }

            var voiceState = Context.User as IVoiceState;
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                if (voiceState?.VoiceChannel == null)
                {
                    await ReplyAsyncWithCheck("Необходимо находиться в голосовом канале!");
                    return;
                }
                if (_lavaNode.TryGetPlayer(Context.Guild, out var botChannel) && (botChannel.VoiceChannel.Id != voiceState?.VoiceChannel.Id))
                {
                    await ReplyAsyncWithCheck("Бот уже находится в голосовом канале: " + _lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name +
                        ", а вы в канале - " + voiceState?.VoiceChannel);
                    return;
                }

                try
                {
                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                }
                catch (Exception exception)
                {
                    await ReplyAsyncWithCheck(exception.Message);
                    return;
                }
            }

            var result = SearchTrack(query).Result;
            await PlayMusicAsync(result);
        }

        [Command("np", RunMode = RunMode.Async)]
        public async Task NowPlayingAsync()
        {
            var player = _lavaNode.GetPlayer(Context.Guild);
            var str = new StringBuilder();
            str.Append($"{player.Track.Title} <{player.Track.Url}>");
            str.AppendLine($" - [{new DateTime(player.Track.Position.Ticks).ToString("HH:mm:ss")}] " +
                $"/[{new DateTime(player.Track.Duration.Ticks).ToString("HH:mm:ss")}]");
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

        /*[Command("Pold", RunMode = RunMode.Async)]
        public async Task PlayAsyncCutOld([Remainder] string query)
        {
            await PlayAsyncOld(query);
        }

        [Command("Playold", RunMode = RunMode.Async)]
        public async Task PlayAsyncOld([Remainder] string query)
        {
            var origQuery = query;
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("Непонятный запрос");
                return;
            }

            var voiceState = Context.User as IVoiceState;

            //var botChannel = _lavaNode.GetPlayer(Context.Guild).VoiceChannel;
            ;

            if (_lavaNode.TryGetPlayer(Context.Guild, out var botChannel) && (botChannel.VoiceChannel.Id != voiceState?.VoiceChannel.Id))
            {
                await ReplyAsyncWithCheck("Бот уже находится в голосовом канале: " + _lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name +
                    ", а вы в канале - " + voiceState?.VoiceChannel);
                return;
            }

            var check = _lavaNode.HasPlayer(Context.Guild);
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
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
            else if (origQuery.Contains("https://music.youtube.com"))
            {
                searchResponse = await _lavaNode.SearchAsync(Victoria.Responses.Search.SearchType.Direct, origQuery);
            }
            else
            {
                searchResponse = await _lavaNode.SearchYouTubeAsync(query);
            }

            LavaTrack? track = null;
            if ((searchResponse.Status == SearchStatus.LoadFailed ||
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
        }*/

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

            player.Queue.Clear();
            await player.StopAsync();
            //await voiceState.VoiceChannel.DisconnectAsync();
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
            //await ReplyAsyncWithCheck($"Пропускаем трек... Сейчас играет **{player.Track.Title}**");

        }

        [Command("q", RunMode = RunMode.Async)]
        public async Task GetQueueAsync()
        {
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


                    str.AppendLine("Всего времени плейлиста: **" + new DateTime(totalTime.Ticks).ToString("HH:mm:ss") + "**");


                    //var queue = "Будущие треки:" + Environment.NewLine + String.Join(Environment.NewLine, player.Queue.Select(x => x.Title));
                    await ReplyAsyncWithCheck(str.ToString());
                }
            }


        }


        [Command("Kick", RunMode = RunMode.Async)]
        private async Task KickAsync()
        {
            var player = _lavaNode.GetPlayer(Context.Guild);

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

            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsyncWithCheck("Бот находится не в ващем текущем голосовом канале");
                return;
            }
            player.Queue.Clear();
            await player.StopAsync();
            await _lavaNode.LeaveAsync(voiceState.VoiceChannel);
            await voiceState.VoiceChannel.DisconnectAsync();
            await ReplyAsyncWithCheck("Бот получил пинок под зад и удалился");

        }

        [Command("shuffle", RunMode = RunMode.Async)]
        private async Task ShuffleAsync()
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsyncWithCheck("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsyncWithCheck("Бот не в голосовом канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsyncWithCheck("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (player.Queue.Count > 1)
            {
                player.Queue.Shuffle();
                await ReplyAsyncWithCheck("Перемешал очередь в случайном порядке!");
            }


        }

        [Command("remove", RunMode = RunMode.Async)]
        public async Task RemoveAsync([Remainder] string number)
        {
            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsyncWithCheck("Вы должны быть в голосовом канале");
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsyncWithCheck("Бот не в голосовм канале");
                return;
            }

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (voiceState.VoiceChannel != player.VoiceChannel)
            {
                await ReplyAsyncWithCheck("Бот находится не в ващем текущем голосовом канале");
                return;
            }

            if (int.TryParse(number, out int n))
            {
                var track = player.Queue.RemoveAt(n);
                await ReplyAsyncWithCheck($"Трек **{track.Title}** удален из очереди");
            }
            else
            {
                await ReplyAsyncWithCheck("Бот не распознал аргумент как номер трека");
                await ReplyAsyncWithCheck("На всякий случай расскажу - надо написать $remove N, где вместо N написать номер трека из команды $q");
            }
        }

        [Command("volume", RunMode = RunMode.Async)]
        private async Task SetVolumeAsync([Remainder] string query)
        {
            if (ushort.TryParse(query, out ushort value))
            {
                if (value > 100 || value < 2)
                {
                    await ReplyAsyncWithCheck($"Громкость надо ставить в пределах от 2 до 100 ");
                    return;
                }
                var player = _lavaNode?.GetPlayer(Context.Guild);
                await player.UpdateVolumeAsync(value);
                await ReplyAsyncWithCheck($"Громкость установлена на " + value);
            }
            else
            {
                await ReplyAsyncWithCheck($"Параметр надо ставить циферкой :) ");
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
            const ulong nickId = 339878121824321536;


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
                    jokesList.Add("Оумаааай.... ");
                    jokesList.Add("О даааа.... ");
                    jokesList.Add("Уляля.... ");
                    jokesList.Add("Влад,  не спать, тут еще пендахос! ");
                    break;
                case oxyId:
                    jokesList.Add("Пипец на холодец! ");
                    jokesList.Add("Мяяяя.... ");
                    jokesList.Add("Я надеюсь ты сейчас в шоколадном бубличке? ");
                    jokesList.Add("Простите, ");
                    jokesList.Add("Вивинг эвэрэйдж, ");
                    jokesList.Add("Жаренные булочки? ");
                    jokesList.Add("Окси, КАКТУС! ");
                    jokesList.Add("Ладушки-оладушки. ");
                    jokesList.Add("Сегодня я буду танчить :smiling_imp:  ");
                    jokesList.Add("17.01 или 01:17? Что-то я запутался уже. ");
                    jokesList.Add("Миотоническая Окси дипсит. ");


                    if (DateTime.Now.Hour > 20)
                    {
                        jokesList.Add("Не ем после шести!!! ");
                    }
                    if (DateTime.Now.Hour > 22)
                    {
                        jokesList.Add("Окси, иди спать! ");
                    }
                    if (DateTime.Now.Month == 1 && DateTime.Now.Day == 17)
                    {
                        jokesList.Clear();
                        jokesList.Add("С Днем Рождения, Окси!:hugging:  С нас печеньки :partying_face: :partying_face: :partying_face: ");
                    }
                    break;
                case ozmaId:
                    jokesList.Add("Фыр-фыр-фыр... ");
                    jokesList.Add("Ваше Лисичество, ");
                    jokesList.Add("Опять спекаться в хила? ");
                    jokesList.Add("Опять спекаться с хила? ");
                    jokesList.Add("Погоди, сейчас переспекаюсь в хила... ");
                    jokesList.Add("What does the fox say? The fox say \"Трында!\". ");
                    if (DateTime.Now.Hour < 9)
                    {
                        jokesList.Clear();
                        jokesList.Add("Ваше Лисичество, ");
                        jokesList.Add("Так рано не спишь? ");
                        jokesList.Add("Уже сегодня или еще вчера? ");
                    }
                    break;
                case juibId:
                    jokesList.Add("Леонид Кагутин, ");
                    jokesList.Add("ММ сегодня не лагает? ");
                    jokesList.Add("Леонид Кагутин, продажи уже просчитались? ");
                    jokesList.Add("Погоди, у меня место в сумке закончилось... ");
                    jokesList.Add("Погоди, у меня место в очереди закончилось... ");
                    jokesList.Add("Релеквин не трогай! (оба) ");
                    break;
                case meddoId:
                    jokesList.Add("Чё началось-то? ");
                    jokesList.Add("Я ничего не трогал! ");
                    jokesList.Add("Уже сегодня или еще вчера? ");
                    jokesList.Add("Ты ничего не трогал? ");
                    break;
                case trimarId:
                    jokesList.Add("За Гомеза! ");
                    jokesList.Add("Вот мои 30 золотых монет. ");
                    break;
                case badfraggId:
                    jokesList.Add("Отдай, ");
                    jokesList.Add("Ваше преступление фотофиксируется. ");
                    jokesList.Add("Вас ждут в Жабском суде! ");
                    jokesList.Add("Вас ждут в Гаагском суде! ");
                    break;
                case falcaId:
                    jokesList.Add("Ништяяяк... ");
                    break;
                case kidneyId:
                    jokesList.Add("Рандом подкручен, признавайся! ");
                    jokesList.Add("35/36 ");
                    jokesList.Add("Сегодня будем бомбить? ");
                    break;
                case falinId:
                    jokesList.Add("Мой создатель, ");
                    jokesList.Add("Мой повелитель, ");
                    jokesList.Add("Милорд, ");
                    break;
                case jutaId:
                    jokesList.Add("Ели мясо оборотнИ, амброзией запивали! ");
                    break;
                case elengelId:
                    jokesList.Add("Шестизначный дипс, кста. ");
                    jokesList.Add("Это уже какая бутылочка коньяка? ");
                    jokesList.Add("Го винишка? ");
                    break;
                case sovaId:
                    jokesList.Add("Совень, забери!!! ");
                    jokesList.Add("Выключите интернет Сове! ");
                    jokesList.Add("Пора менять сим-карту? ");
                    jokesList.Add("Пора дипсить! ");
                    jokesList.Add("Пора переходить на 3g ");
                    break;
                case elizabethId:
                    jokesList.Add("Если есть в кармане пачка... Ой, простите, нету пачки ");
                    break;
                case minorisId:
                    jokesList.Add("Уже пора править график? ");
                    jokesList.Add("Профессиональный занудка, ");
                    jokesList.Add("Зачем мне микрофон? И так слышно ");
                    break;
                case nickId:
                    jokesList.Add("Где мой инсулин? ");
                    jokesList.Add("При чем тут паравозик Томас? ");
                    jokesList.Add(":nerd: ? ");
                    jokesList.Add(":eyes: ? ");
                    break;
                default:
                    break;
            }

            if (jokesList.Count == 0)
            {
                await ReplyAsync(message);
                return;
            }
            else if ((DateTime.Now.Month == 12 && DateTime.Now.Day == 31) || (DateTime.Now.Month == 1 && DateTime.Now.Day == 1 && DateTime.Now.Hour <= 6))
            {
                jokesList.Clear();
                jokesList.Add("Сегодня все особенное! ");
                jokesList.Add("28 ударов ножом... А ты уже нарезал оливье? ");
                jokesList.Add("В этот особый Новый Год я сделаю вам особый подарок - скидку на Скайрим :) ");
                jokesList.Add("Здешние пески холодные, но когда ты здесь, FoxyBot`у становится теплее :)  ");
                jokesList.Add("Пусть дорога приведет тебя в... Так. В Эльсвеере драконы. В Скайриме вампиры. Короче сиди в этот Новый Год в норе с друзьями :) ");
                jokesList.Add("Эх, вот бы сейчас в Аргентину... А лучше Влада туда отправить. ");
                jokesList.Add("В этот Новый Год я желаю вам всем не забывать выходить из АОЕ!");
                jokesList.Add("В этот Новый Год я желаю вам всем не забывать подбирать Олориму!");
                jokesList.Add("В этот Новый Год я желаю вам всем отличного вивинг эверейдж! ");
                jokesList.Add("В этот Новый Год я желаю вам всем что бы всегда хватало маны! ");
                jokesList.Add("В этот Новый Год я желаю вам всем что бы всегда хватало стамины! ");
                jokesList.Add("В этот Новый Год я желаю вам всем что бы здоровье не заканчивалось! ");
                jokesList.Add("В этот Новый Год я желаю вам всем что бы в хард контенте всегда рядом был хил! ");
                jokesList.Add("В этот Новый Год я желаю вам всем что бы танк не терял агр! ");
                jokesList.Add("В этот Новый Год я желаю вам всем что бы для вас всегда были дешевые кроны! ");
                jokesList.Add("В этот Новый Год я желаю вам всем нормально дипсящих рандомов ");
                jokesList.Add("В этот Новый Год я желаю вам всем второго ГСа, кста! ");
                jokesList.Add("В этот Новый Год я желаю вам всем получить скидку на пейран от Влада! ");
                jokesList.Add("Сегодня хороший праздник! Давайте понизим уровень токсичности, залив его шампусиком)) ");
                jokesList.Add("давайте бахнем шампусика и пойдем искать грудь королевы Айрен? ");

            }

            await ReplyAsync(jokesList.ElementAt(random.Next(jokesList.Count)) + message);
        }


        private async Task PlayMusicAsync(LavaTrack track)
        {
            await PlayMusicAsync(new List<LavaTrack>(new[] { track }));
        }

        private async Task PlayMusicAsync(List<LavaTrack> trackList, bool playNext = false)
        {
            if (trackList.Count == 0)
            {
                await ReplyAsyncWithCheck($"К сожалению у меня не получилось найти нужное :pleading_face: ");
                return;
            }

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                if (trackList.Count > 1)
                {
                    foreach (var track in trackList)
                    {
                        player?.Queue.Enqueue(track);
                    }

                    await ReplyAsyncWithCheck($"Добавлено в очередь -> **{trackList.Count} треков**");
                }
                else if (trackList.Count == 1)
                {
                    if (playNext)
                    {
                        var tmpQueue = player.Queue.ToList();
                        tmpQueue.Insert(0, trackList.First());
                        player.Queue.Clear();
                        foreach (var item in tmpQueue)
                        {
                            player.Queue.Enqueue(item);
                        }

                    }
                    else
                    {
                        player.Queue.Enqueue(trackList.First());
                    }

                    await ReplyAsyncWithCheck($"Добавлено в очередь -> **{trackList.First().Title}**");
                }

                return;
            }
            else
            {
                await player.PlayAsync(trackList.ElementAt(0));
                await ReplyAsyncWithCheck($"Сейчас играет -> **{trackList.ElementAt(0).Title}**");
                trackList.RemoveAt(0);
                if (trackList.Count == 0)
                    return;
                foreach (var track in trackList)
                {
                    player?.Queue.Enqueue(track);
                }

                await ReplyAsyncWithCheck($"Добавлено в очередь -> **{trackList.Count} треков**");

            }
        }

        private async Task<List<LavaTrack>> SearchTrack(string query)
        {
            List<LavaTrack> trackList = new List<LavaTrack>();

            if (query.Contains("youtu.be") || query.Contains("youtube.com"))
            {
                trackList = await SearchTrackUri(query);
            }
            else if (int.TryParse(query, out var number))
            {
                trackList = await SearchTrackNumber(number);
            }
            else
            {
                trackList = await SearchTrackString(query);
            }

            return trackList;
        }

        private async Task<List<LavaTrack>?> SearchTrackUri(string query)
        {

            var uri = new Uri(query);
            var id = HttpUtility.ParseQueryString(uri.Query).Get("v");

            if (id == null)
            {
                id = uri.LocalPath.Trim('/').Split('?')[0];
            }


            if (uri.Host == "music.youtube.com")
            {
                var searchString = $"http://{uri.Host}/watch?v={id}";
                var res = await _lavaNode.SearchAsync(SearchType.Direct, searchString);

                if (res.Status == SearchStatus.LoadFailed)
                {
                    res = await _lavaNode.SearchAsync(SearchType.YouTubeMusic, searchString);
                    if (res.Status == SearchStatus.LoadFailed)
                    {
                        var count = 0;
                        while (res.Status != SearchStatus.TrackLoaded || res.Status != SearchStatus.PlaylistLoaded)
                        {
                            res = await _lavaNode.SearchAsync(SearchType.Direct, searchString);
                            count++;

                            if (count == 10)
                            {
                                break;
                            }
                        }
                    }



                }

                return res.Tracks.ToList();
            }
            else if (uri.Host == "youtube.com" || uri.Host == "www.youtube.com" || uri.Host == "youtu.be")
            {
                var list = HttpUtility.ParseQueryString(uri.Query).Get("list");
                var index = HttpUtility.ParseQueryString(uri.Query).Get("index");

                // Если это плейлист
                if (list != null)
                {
                    var str = "https://youtu.be/" + id + "?list=" + list + "&index=" + index;
                    var res = await _lavaNode.SearchAsync(SearchType.Direct, str);

                    if (res.Status == SearchStatus.LoadFailed || res.Status == SearchStatus.NoMatches)
                    {
                        str = "https://youtu.be/" + id;
                        res = await _lavaNode.SearchAsync(SearchType.Direct, str);

                        if (res.Status == SearchStatus.LoadFailed || res.Status == SearchStatus.NoMatches)
                            await ReplyAsyncWithCheck($"Поиск завершился ошибкой: {res.Exception.Message}");
                    }

                    var tracks = res.Tracks.ToList();
                    return tracks.GetRange(res.Playlist.SelectedTrack, res.Tracks.Count - res.Playlist.SelectedTrack);
                }
                else // Если это ссылка на видео
                {
                    _logger.LogDebug($"Search track with id: {id}");
                    //var searchString = $"http://{uri.Host}/watch?v={id}";
                    var searchString = id;
                    var res = await _lavaNode.SearchAsync(SearchType.Direct, searchString);
                    if (res.Status == SearchStatus.NoMatches)
                    {
                        _logger.LogDebug($"NoMatches, try search with full url...");
                        searchString = $"http://{uri.Host}/watch?v={id}";
                        res = await _lavaNode.SearchAsync(SearchType.Direct, searchString);
                    }
                    var track = new List<LavaTrack>();
                    foreach (var item in res.Tracks)
                    {
                        if (item.Id == id)
                        {
                            track.Add(item);
                            break;
                        }
                    }

                    return track;
                }


            }

            return new List<LavaTrack>();
        }

        private async Task<List<LavaTrack>> SearchTrackString(string query)
        {
            var list = new List<LavaTrack>();
            var res = await _lavaNode.SearchAsync(SearchType.YouTube, query);
            if (res.Status == SearchStatus.LoadFailed)
            {
                res = await _lavaNode.SearchAsync(SearchType.YouTube, query);
            }
            else if (res.Status == SearchStatus.NoMatches)
            {
                res = await _lavaNode.SearchAsync(SearchType.Direct, query);
                if (res.Status == SearchStatus.NoMatches)
                {
                    res = await _lavaNode.SearchAsync(SearchType.SoundCloud, query);
                }

            }
            if (res.Tracks.Count >= 1)
                list.Add(res.Tracks.First());
            return list;
        }

        private async Task<List<LavaTrack>> SearchTrackNumber(int number)
        {
            var messages = Context.Channel.GetCachedMessages(10);
            string query = "";

            foreach (var message in messages)
            {
                if (message.Content.ToLower().Contains("$search"))
                {
                    query = message.Content.TrimStart("$search".ToCharArray());
                    break;
                }
            }
            if (query != "")
            {
                var res = await _lavaNode.SearchAsync(SearchType.YouTube, query);
                var trackList = new List<LavaTrack>(new[] { res.Tracks.ElementAt(number) });

                return trackList;
            }

            return new List<LavaTrack>();
        }
    }
}

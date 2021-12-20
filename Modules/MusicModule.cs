using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Web;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;
using FoxyBot.Spotify;
using FoxyBot.Spotify.Recomendations;
using Microsoft.Extensions.Configuration;

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
        }

        [Command("help", RunMode = RunMode.Async)]
        public async Task HelpAsync()
        {
            await ReplyAsyncWithCheck(@"рассказываю как мною пользоваться:
play - p - поиск на ютубе
pn - поставить трек следующим в очереди после сейчас проигрываемого
pl - добавить к воспроизведению плейлист
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

track - поиск треков для последующей генерации рандомного плейлиста. Нужно писать только название трека, без назввания артиста
follow - добавить в очередь треки, похожие на выбранный
Как пользоваться:
1) $track Never Gonna Give You Up (Official Music Video)
Получаем выдачу из 20 элементов
2) $follow 2
Треки добавляются в очередь. Это занимает какое тов время (от 10 секунд обычно)

Если бот внезапно лагает (запинается музыка), то скорее всего виноват музыкальный сервер. Поменять его можно командой $lava N где N - номер сервера
Список доступных серверов можно посмотреть командой $lava
");
        }


        [Command("Clear", RunMode = RunMode.Async)]
        private async Task ClearAsync()
        {
            if (!CheckStateAsync(PlayerState.None).Result)
                return;

            var player = _lavaNode?.GetPlayer(Context.Guild);
            if (player != null)
            {
                player.Queue.Clear();
                await ReplyAsyncWithCheck("очередь очищенна");
            }
        }


        [Command("Seek", RunMode = RunMode.Async)]
        private async Task SeekAsync([Remainder] string query)
        {


            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("совершенно непонятный запрос");
                return;
            }

            if (!CheckStateAsync(PlayerState.None).Result) return;
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) return;
            if (player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsyncWithCheck("я сейчас не играю музыку");
                return;
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
                        await ReplyAsyncWithCheck("не понимаю на какой момент надо перемотать :(");
                    }

                    break;
                case 2:
                    if (!int.TryParse(times[0], out minutes) &
                        !int.TryParse(times[1], out sec))
                        await ReplyAsyncWithCheck("не понимаю на какой момент надо перемотать :(");

                    break;
                case 1:
                    if (!int.TryParse(times[0], out sec))
                        await ReplyAsyncWithCheck("не понимаю на какой момент надо перемотать :(");
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
            await ReplyAsyncWithCheck($"перемотал на {query}");
        }


        [Command("pn", RunMode = RunMode.Async)]
        private async Task PlayNextAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("совершенно непонятный запрос");
                return;
            }
            if (!CheckStateAsync(PlayerState.Playing).Result) return;

            try
            {
                if (Context.User is not IVoiceState voiceState) return;
                await _lavaNode.JoinAsync(voiceState?.VoiceChannel, Context.Channel as ITextChannel);
            }
            catch (Exception exception)
            {
                await ReplyAsyncWithCheck(exception.Message);
                return;
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
                await ReplyAsyncWithCheck("совершенно непонятный запрос");
                return;
            }

            if (!CheckStateAsync(PlayerState.Playing).Result) return;

            try
            {
                if (Context.User is not IVoiceState voiceState) return;
                await _lavaNode.JoinAsync(voiceState?.VoiceChannel, Context.Channel as ITextChannel);
            }
            catch (Exception exception)
            {
                await ReplyAsyncWithCheck(exception.Message);
                return;
            }

            if (Uri.TryCreate(query, UriKind.Absolute, out Uri uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                var id = HttpUtility.ParseQueryString(uri.Query).Get("v");
                var list = HttpUtility.ParseQueryString(uri.Query).Get("list");
                if (list != null)
                {
                    await ReplyAsyncWithCheck("тут ссылка на плейлист, но это команда для добавления одного трека. Если надо добавить плейлист - используйте $pl (это буква л английская)");
                }
            }

            var result = SearchTrack(query, false).Result;
            await PlayMusicAsync(result);
        }

        [Command("Pl", RunMode = RunMode.Async)]
        private async Task PlayPlaylistAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsyncWithCheck("совершенно непонятный запрос");
                return;
            }

            if (!CheckStateAsync(PlayerState.Playing).Result) return;

            try
            {
                if (Context.User is not IVoiceState voiceState) return;
                await _lavaNode.JoinAsync(voiceState?.VoiceChannel, Context.Channel as ITextChannel);
            }
            catch (Exception exception)
            {
                await ReplyAsyncWithCheck(exception.Message);
                return;
            }

            var result = SearchTrack(query, true).Result;
            await PlayMusicAsync(result);
        }


        [Command("np", RunMode = RunMode.Async)]
        public async Task NowPlayingAsync()
        {
            var player = _lavaNode.GetPlayer(Context.Guild);
            var str = new StringBuilder();
            str.Append($"сейчас играет **{player.Track.Title}** <{player.Track.Url}>");
            str.AppendLine($" - [{new DateTime(player.Track.Position.Ticks):HH:mm:ss}] " +
                $"/[{new DateTime(player.Track.Duration.Ticks):HH:mm:ss}]");
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
                await ReplyAsyncWithCheck("я получил непонятный запрос");
                return;
            }

            if (!CheckStateAsync(null).Result) return;
            if (query.Contains("https://") || query.Contains("http://"))
            {
                await ReplyAsyncWithCheck($"не надо искать ссылки");
                return;
            }

            var searchResponse = await _lavaNode.SearchYouTubeAsync(query);
            if (searchResponse.Status == SearchStatus.LoadFailed ||
                searchResponse.Status == SearchStatus.NoMatches)
            {
                await ReplyAsyncWithCheck($"ничего не найдено по запросу `{query}`.");
                return;
            }

            var str = new StringBuilder();
            str.AppendLine("вот что я нашел:");

            for (int i = 0; i < searchResponse.Tracks.Count; i++)
            {
                str.AppendLine($"{i} - {searchResponse.Tracks.ElementAt(i).Title} [{new DateTime(searchResponse.Tracks.ElementAt(i).Duration.Ticks):HH:mm:ss}]");
            }

            await ReplyAsyncWithCheck(str.ToString());
        }


        [Command("Stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            if (!CheckStateAsync(PlayerState.Stopped).Result) return;

            var player = _lavaNode.GetPlayer(Context.Guild);

            if (player.PlayerState == PlayerState.Paused)
            {
                await ReplyAsyncWithCheck("музыка была на паузе, я ее остановил и очистил очередь");
            }
            player.Queue.Clear();
            await player.StopAsync();
        }


        [Command("Pause", RunMode = RunMode.Async)]
        public async Task PauseAsync()
        {
            if (!CheckStateAsync(PlayerState.Paused).Result) return;


            var player = _lavaNode.GetPlayer(Context.Guild);
            await player.PauseAsync();
            await ReplyAsyncWithCheck("ставлю музыку на паузу... Время сходить за печеньками?");
        }


        [Command("Resume", RunMode = RunMode.Async)]
        public async Task ResumeAsync()
        {
            if (!CheckStateAsync(PlayerState.None).Result) return;
            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player.PlayerState != PlayerState.Paused)
            {
                await ReplyAsyncWithCheck($"я не на паузе");
                return;
            }
            await player.ResumeAsync();
            await ReplyAsyncWithCheck($"продолжаю воспроизведение");
        }


        [Command("Skip", RunMode = RunMode.Async)]
        public async Task SkipAsync()
        {
            if (!CheckStateAsync(PlayerState.None).Result) return;

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player.Queue.Count == 0)
            {
                await player.StopAsync();
                return;
            }

            await player.SkipAsync();
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
                        str.Append($"сейчас играет: **{player.Track.Title}** ");
                        str.AppendLine($"Осталось [{new DateTime((player.Track.Duration - player.Track.Position).Ticks):HH:mm:ss}] " +
                        $"<{player.Track.Url}>");
                    }

                    for (int i = 0; i < player.Queue.Count; i++)
                    {
                        str.AppendLine($"{i} - **{player.Queue.ElementAt(i).Title}** [{new DateTime(player.Queue.ElementAt(i).Duration.Ticks):HH:mm:ss}] " +
                            $"<{ player.Queue.ElementAt(i).Url}>");

                        if (i >= 10)
                        {
                            str.AppendLine("Там есть еще треки, но они не влезли в сообщение");
                            break;
                        }
                    }
                    var q = new List<TimeSpan>();
                    q.AddRange(player.Queue.Select(x => x.Duration).ToList());
                    q.Add(player.Track.Duration - player.Track.Position);
                    var totalTime = q.Aggregate
                                    (TimeSpan.Zero,
                                    (sumSoFar, nextMyObject) => sumSoFar + nextMyObject);


                    str.AppendLine("Всего времени плейлиста: **" + new DateTime(totalTime.Ticks).ToString("HH:mm:ss") + "**");
                    await ReplyAsyncWithCheck(str.ToString());
                }
            }


        }


        [Command("Kick", RunMode = RunMode.Async)]
        private async Task KickAsync()
        {
            if (!CheckStateAsync(PlayerState.None).Result) return;

            if (Context.User is not IVoiceState voiceState) return;
            var player = _lavaNode.GetPlayer(Context.Guild);
            player.Queue.Clear();
            await player.StopAsync();
            await _lavaNode.LeaveAsync(voiceState.VoiceChannel);
            await voiceState.VoiceChannel.DisconnectAsync();
            await ReplyAsyncWithCheck("бот получил пинок под зад и удалился");

        }


        [Command("shuffle", RunMode = RunMode.Async)]
        private async Task ShuffleAsync()
        {
            if (!CheckStateAsync(PlayerState.None).Result) return;

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (player.Queue.Count > 1)
            {
                player.Queue.Shuffle();
                await ReplyAsyncWithCheck("я перемешал очередь в случайном порядке!");
            }
            else
            {
                await ReplyAsyncWithCheck("очередь пустая, там нечего перемешивать");
            }
        }


        [Command("remove", RunMode = RunMode.Async)]
        public async Task RemoveAsync([Remainder] string number)
        {
            if (!CheckStateAsync(PlayerState.None).Result) return;

            var player = _lavaNode.GetPlayer(Context.Guild);
            if (int.TryParse(number, out int n))
            {
                if (n > player.Queue.Count || n <= -1)
                {
                    await ReplyAsyncWithCheck($"такого трека нет в очереди");
                    return;
                }
                var track = player.Queue.RemoveAt(n);
                await ReplyAsyncWithCheck($"трек **{track.Title}** удален из очереди");
            }
            else
            {
                await ReplyAsyncWithCheck("бот не распознал аргумент как номер трека");
                await ReplyAsyncWithCheck("на всякий случай расскажу - надо написать $remove N, где вместо N написать номер трека из команды $q");
            }
        }


        [Command("volume", RunMode = RunMode.Async)]
        private async Task SetVolumeAsync([Remainder] string query)
        {
            if (!CheckStateAsync(PlayerState.None).Result) return;

            if (ushort.TryParse(query, out ushort value))
            {
                if (value > 100 || value < 2)
                {
                    await ReplyAsyncWithCheck("громкость надо ставить в пределах от 2 до 100 ");
                    return;
                }
                //var player = _lavaNode?.GetPlayer(Context.Guild);
                if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                    return;
                await player.UpdateVolumeAsync(value);
                await ReplyAsyncWithCheck($"громкость установлена на " + value);
            }
            else
            {
                await ReplyAsyncWithCheck($"параметр надо ставить циферкой :) ");
            }
        }


        [Command("track", RunMode = RunMode.Async)]
        private async Task SpotifySearchAsync([Remainder] string query)
        {



            var sb = new StringBuilder();
            sb.AppendLine("Я нашел следующие треки:");

            SpotifySearchResponce json = await SpotifySearch(query, "track");

            for (int i = 0; i < json.tracks.items.Count; i++)
            {
                if (json.tracks.items[i].artists.Count == 0) continue;
                sb.AppendLine($"{i} - {json.tracks.items[i].artists.First().name} - {json.tracks.items[i].name} ||SpotifyID:{json.tracks.items[i].id}||");
            }

            await ReplyAsync(sb.ToString());
        }

        [Command("follow", RunMode = RunMode.Async)]
        private async Task FollowTrack([Remainder] string query)
        {
            var messages = Context.Channel.GetCachedMessages(50);

            if (!int.TryParse(query, out var id)) return;


            foreach (var message in messages)
            {
                if (message.Content.Contains("Я нашел следующие треки:") && message.Author.Id == 887228176135249980)
                {
                    await ReplyAsync("Добавляю треки в очередь...");

                    var str = message.Content;
                    var allLines = str.Split(Environment.NewLine).ToList();
                    allLines.RemoveAt(0);

                    if (id < 0 || id > allLines.Count - 1)
                    {
                        await ReplyAsync("Нет там таких треков!");
                        return;
                    }

                    var trackId = allLines[id].Substring(0, allLines[id].Length - 2).Split(':').ToList().Last();

                    var listOfTracksSpotify = await SpotifySearchRecommendations(trackId);
                    var listLavaTracks = new List<LavaTrack>();

                    if (listOfTracksSpotify.Count > 0)
                    {
                        try
                        {
                            if (Context.User is not IVoiceState voiceState) return;
                            await _lavaNode.JoinAsync(voiceState?.VoiceChannel, Context.Channel as ITextChannel);
                        }
                        catch (Exception exception)
                        {
                            await ReplyAsyncWithCheck(exception.Message);
                            return;
                        }

                        var t = await SearchTrackString(listOfTracksSpotify[0]);
                        await PlayMusicAsync(t);
                        listOfTracksSpotify.RemoveAt(0);
                        await Task.Delay(2000);
                    }

                    foreach (var track in listOfTracksSpotify)
                    {
                        var t = await SearchTrackString(track);
                        //await Task.Delay(TimeSpan.FromSeconds(2));
                        listLavaTracks.Add(t.First());
                        //await PlayMusicAsync(t);
                    }

                    await PlayMusicAsync(listLavaTracks);

                    break;
                }
            }
        }


        public static async Task<string> GetToken()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            RestClient client = new RestClient("https://accounts.spotify.com/");
            var client_id = configuration.GetValue<string>("client_id");
            var client_secret = configuration.GetValue<string>("client_secret");

            var request = new RestRequest("api/token", Method.POST);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Authorization", "Basic " + Base64Encode($"{client_id}:{client_secret}"));
            request.AddParameter("grant_type", "client_credentials");

            var result = client.Execute(request);

            return JsonConvert.DeserializeObject<Login>(result.Content).access_token;
        }


        public static async Task<List<string>> SpotifySearchRecommendations(string track_id)
        {
            var token = await GetToken();
            RestClient client = new RestClient("https://api.spotify.com/");
            client.Authenticator = new JwtAuthenticator(token);

            var request = new RestRequest("v1/recommendations/", Method.GET);
            request.AddParameter("seed_tracks", track_id);

            var result = await client.ExecuteAsync(request);
            var recommendations = JsonConvert.DeserializeObject<SpotifyRecommendation>(result.Content);

            var list = new List<string>();

            foreach (var item in recommendations.tracks)
            {
                list.Add(item.artists.First().name + " - " + item.name);
            }


            return list;
        }


        public static async Task<SpotifySearchResponce> SpotifySearch(string search, string searchType)
        {
            var token = await GetToken();

            RestClient client = new RestClient("https://api.spotify.com/");
            client.Authenticator = new JwtAuthenticator(token);

            var result = await SpotifySearchAsync(client, search, searchType);

            var obj = JsonConvert.DeserializeObject<SpotifySearchResponce>(result.Content);
            return obj;
        }


        public static async Task<IRestResponse> SpotifySearchAsync(RestClient client, string search, string searchType)
        {
            var request = new RestRequest("v1/search/", Method.GET);
            request.AddParameter("q", search);
            request.AddParameter("type", searchType);

            return await client.ExecuteAsync(request);
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
                    jokesList.Add("Найс демедж,найс баланс, ");
                    jokesList.Add("Оумаааай.... ");
                    jokesList.Add("О даааа.... ");
                    jokesList.Add("Уляля.... ");
                    jokesList.Add("Влад,  не спать, тут еще пендахос! ");
                    break;
                case oxyId:
                    jokesList.Add("Пипец на холодец! Окси, ");
                    jokesList.Add("Мяяяя.... ");
                    jokesList.Add("Я надеюсь ты сейчас в шоколадном бубличке? А то ");
                    jokesList.Add("Простите, ");
                    jokesList.Add("Вивинг эвэрэйдж, ");
                    jokesList.Add("Жаренные булочки, ");
                    jokesList.Add("Окси, КАКТУС! Срочно, а то ");
                    jokesList.Add("Ладушки-оладушки, ");
                    jokesList.Add("Сегодня я буду танчить :smiling_imp: и к тому же ");
                    jokesList.Add("17.01 или 01:17? Что-то я запутался уже. А пока что ");
                    jokesList.Add("Миотоническая Окси дипсит. ");

                    if (DateTime.Now.Hour > 22)
                    {
                        jokesList.Add("Окси, иди спать! ");
                    }
                    if (DateTime.Now.Month == 1 && DateTime.Now.Day == 17)
                    {
                        jokesList.Clear();
                        jokesList.Add("С Днем Рождения, Окси!:hugging:  С нас печеньки :partying_face: :partying_face: :partying_face: ");
                        jokesList.Add("С Днем Рождения, Окси!:hugging: ");
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
                    jokesList.Add("ММ сегодня не лагает, ");
                    jokesList.Add("Леонид Кагутин, продажи уже просчитались, ");
                    jokesList.Add("Погоди, у меня место в сумке закончилось... ");
                    jokesList.Add("Погоди, у меня место в очереди закончилось... ");
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
                    jokesList.Add("Сегодня будем бомбить, ");
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
                    jokesList.Add("Шестизначный дипс, кста, ");
                    jokesList.Add("Это уже какая бутылочка коньяка? ");
                    jokesList.Add("Го винишка? ");
                    break;
                case sovaId:
                    jokesList.Add("Совень, забери!!! ");
                    jokesList.Add("Пора менять сим-карту? ");
                    jokesList.Add("Пора дипсить! ");
                    jokesList.Add("Пора переходить на 3g, ");
                    break;
                case elizabethId:
                    jokesList.Add("Если есть в кармане пачка... Ой, простите, нету пачки, ");
                    break;
                case minorisId:
                    jokesList.Add("Уже пора править график? ");
                    jokesList.Add("Профессиональный занудка, ");
                    jokesList.Add("Зачем микрофон? И так слышно, ");
                    break;
                case nickId:
                    jokesList.Add("Где мой инсулин? ");
                    jokesList.Add(":nerd: ? ");
                    jokesList.Add(":eyes: ? ");
                    break;
                default:
                    jokesList.Add("Норный житель, ");
                    jokesList.Add("Человек, ");
                    break;
            }
            if ((DateTime.Now.Month == 12 && DateTime.Now.Day >= 24) || (DateTime.Now.Month == 1 && DateTime.Now.Day <= 8 && DateTime.Now.Hour <= 6))
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
                jokesList.Add("Давайте бахнем шампусика и пойдем искать грудь королевы Айрен? ");

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
                await ReplyAsyncWithCheck("к сожалению у меня не получилось найти нужное :pleading_face: ");
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

                    await ReplyAsyncWithCheck($"добавил в очередь -> **{trackList.Count} треков**");
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

                    await ReplyAsyncWithCheck($"добавил в очередь -> **{trackList.First().Title}**");
                }

                return;
            }
            else
            {
                await player.PlayAsync(trackList.ElementAt(0));
                await ReplyAsyncWithCheck($"сейчас играет -> **{trackList.ElementAt(0).Title}**");
                trackList.RemoveAt(0);
                if (trackList.Count == 0)
                    return;
                foreach (var track in trackList)
                {
                    player?.Queue.Enqueue(track);
                }

                await ReplyAsyncWithCheck($"добавлено в очередь -> **{trackList.Count} треков**");

            }
        }


        private async Task<List<LavaTrack>> SearchTrack(string query, bool allowPlaylist = false)
        {
            List<LavaTrack>? trackList = new List<LavaTrack>();

            if (query.Contains("youtu.be") || query.Contains("youtube.com"))
            {
                trackList = await SearchTrackUri(query, allowPlaylist);
            }
            else if (ushort.TryParse(query, out var number))
            {
                trackList = await SearchTrackNumber(number);
            }
            else
            {
                trackList = await SearchTrackString(query);
            }

            return trackList;
        }


        private async Task<List<LavaTrack>?> SearchTrackUri(string query, bool allowPlaylist = false)
        {

            var uri = new Uri(query);
            var id = HttpUtility.ParseQueryString(uri.Query).Get("v");
            var list = HttpUtility.ParseQueryString(uri.Query).Get("list");
            var index = HttpUtility.ParseQueryString(uri.Query).Get("index");

            if (id == null)
                id = uri.LocalPath.Trim('/').Split('?')[0];

            var searchString = "";
            if (allowPlaylist && list != null)
                searchString = "https://youtu.be/" + id + "?list=" + list + "&index=" + index;
            else
                searchString = $"http://{uri.Host}/watch?v={id}";

            var res = await _lavaNode.SearchAsync(SearchType.Direct, searchString);
            if (res.Status == SearchStatus.LoadFailed || res.Status == SearchStatus.NoMatches)
                res = await _lavaNode.SearchAsync(SearchType.YouTube, searchString);
            if (res.Status == SearchStatus.LoadFailed)
                await ReplyAsyncWithCheck($"Поиск завершился ошибкой: {res.Exception.Message}");

            if (allowPlaylist)
            {
                var tracks = res.Tracks.ToList();
                return tracks.GetRange(res.Playlist.SelectedTrack, res.Tracks.Count - res.Playlist.SelectedTrack);
            }
            else
            {
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


            /*
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
            */
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


        private async Task<List<LavaTrack>> SearchTrackNumber(ushort number)
        {
            var messages = Context.Channel.GetCachedMessages(50);
            string query = "";
            var dict = new Dictionary<ushort, string>();

            foreach (var message in messages)
            {
                if (message.Content.Contains("вот что я нашел:") && message.Author.Id == 887228176135249980)
                {
                    var str = message.Content;
                    var allLines = str.Split(Environment.NewLine).ToList();
                    allLines.RemoveAt(0);

                    foreach (string line in allLines)
                    {
                        var n = new String(line.TakeWhile(Char.IsDigit).ToArray());
                        if (!ushort.TryParse(n, out var id))
                        {
                            _logger.LogDebug($"Не получилось преобразовать строку {n} в число");
                            continue;
                        }
                        var name = line.Substring(n.Length + 3, line.Length - (n.Length + 3) - 11);
                        dict.Add(id, name);
                    }
                    break;
                }
            }
            if (number < 0 || number > dict.Count - 1)
            {
                await ReplyAsyncWithCheck("я таких цифр не говорил!");
                return new List<LavaTrack>();
            }
            query = dict[number];
            var res = await _lavaNode.SearchAsync(SearchType.YouTube, query);

            foreach (var t in res.Tracks)
            {
                if (t.Title == query)
                {
                    return new List<LavaTrack>(new[] { t });
                }
            }
            await ReplyAsyncWithCheck("по какой-то причине на этот ютуб не нашел трек с именно таким названием, я поставлю наиболее релевантный ответ");
            var track = res.Tracks.FirstOrDefault();
            if (track != null)
                return new List<LavaTrack>(new[] { track });
            return new List<LavaTrack>();
        }


        // Передаем то состояние, в которое хотим перевести плеер
        private async Task<bool> CheckStateAsync(PlayerState? needPlayerState = null)
        {
            var voiceState = Context.User as IVoiceState;                                  // Состояние пользователя
            _lavaNode.TryGetPlayer(Context.Guild, out var player);                        // Состояние бота
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsyncWithCheck("необходимо находиться в голосовом канале!");
                return false;
            }

            //Проверим что бот не в другом канале
            if (needPlayerState == PlayerState.Playing)
            {
                if (player != null && player?.VoiceChannel.Id != voiceState?.VoiceChannel.Id)
                {
                    await ReplyAsyncWithCheck("бот уже находится в голосовом канале: " + _lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name +
                                                ", а вы в канале - " + voiceState?.VoiceChannel);
                    return false;
                }

            }
            // Проверим что пользователь в голосовом канале, бот не в другом канале и бот уже не молчит
            else if (needPlayerState == PlayerState.Stopped || needPlayerState == PlayerState.Paused)
            {
                if (player == null)
                {
                    await ReplyAsyncWithCheck("я не в голосовом канале вообще!");
                    return false;
                }
                if (player.VoiceChannel.Id != voiceState?.VoiceChannel.Id)
                {
                    await ReplyAsyncWithCheck("я уже нахожусь в голосовом канале: " + _lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name +
                                                ", а вы в канале - " + voiceState?.VoiceChannel);
                    return false;
                }
                if (needPlayerState == PlayerState.Stopped && player.PlayerState == PlayerState.Paused)
                {
                    return true;
                }
                if (player.PlayerState == PlayerState.Stopped || player.PlayerState == PlayerState.Paused || player.PlayerState == PlayerState.None)
                {
                    await ReplyAsyncWithCheck("я и так молчу!");
                    return false;
                }
            }
            else if (needPlayerState == PlayerState.None) // Просто проверим что пользователь в одном канале с ботом
            {
                if (player == null)
                {
                    await ReplyAsyncWithCheck("я не в голосовом канале вообще!");
                    return false;
                }
                if (player.VoiceChannel.Id != voiceState?.VoiceChannel.Id)
                {
                    await ReplyAsyncWithCheck("я уже нахожусь в голосовом канале: " + _lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name +
                                                ", а вы в канале - " + voiceState?.VoiceChannel);
                    return false;
                }
            }
            else if (needPlayerState == null)
            {
                return true;
            }

            return true;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}
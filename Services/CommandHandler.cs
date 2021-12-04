using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace FoxyBot.Services
{
    public class CommandHandler : DiscordClientService
    {

        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _service;
        private readonly IConfiguration _configuration;
        private readonly LavaNode _lavaNode;
        public static ConcurrentDictionary<ulong, int> serverFailCount = new ConcurrentDictionary<ulong, int>();
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();
        private readonly int timeout = 300;

        ILogger<DiscordClientService> _logger;

        public CommandHandler(DiscordSocketClient client, ILogger<DiscordClientService> logger, LavaNode lavaNode, IServiceProvider provider,
            CommandService service, IConfiguration configuration) : base(client, logger)
        {
            _client = client;
            _logger = logger;
            _lavaNode = lavaNode;
            _service = service;
            _provider = provider;
            _configuration = configuration;
        }


        private async Task Client_Ready()
        {
            if (!_lavaNode.IsConnected)
            {
                await _lavaNode.ConnectAsync();
            }
        }


        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += OnMessageReceived;
            _client.Ready += Client_Ready;
            _lavaNode.OnTrackEnded += _lavaNode_OnTrackEnded;
            _lavaNode.OnTrackStarted += _lavaNode_OnTrackStarted;
            _lavaNode.OnTrackStuck += _lavaNode_OnTrackStuck;
            _client.SetGameAsync(" норке");
            await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }


        private async Task _lavaNode_OnTrackStuck(TrackStuckEventArgs arg)
        {
            await arg.Player.TextChannel.SendMessageAsync("Трек застрял? Попробуйте $kick и поставить его заново");
        }


        private async Task OnMessageReceived(SocketMessage socketMessage)
        {
            if (socketMessage is not SocketUserMessage message) return;
            if (message.Source != Discord.MessageSource.User) return;

            var argPos = 0;
            if (!message.HasStringPrefix(_configuration["Prefix"], ref argPos) && !message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;

            var context = new SocketCommandContext(_client, message);
            await _service.ExecuteAsync(context, argPos, _provider);

        }

        private async Task _lavaNode_OnTrackEnded(TrackEndedEventArgs arg)
        {
            var guild = arg.Player.VoiceChannel.Guild.Id;
            var player = arg.Player;

            // Переподключение трека в случае ошибки
            if (arg.Reason == TrackEndReason.LoadFailed)
            {
                if (serverFailCount.ContainsKey(guild))
                    serverFailCount[guild]++;
                else
                    serverFailCount[guild] = 0;

                if (serverFailCount[guild] >= 5)
                {
                    await arg.Player.TextChannel.SendMessageAsync($"**{arg.Track.Title}** вызвала ошибку LoadFailed и я все же решил ее пропустить");

                    if (player.Track != null)
                    {
                        await CancelDisconnect(arg);
                        await arg.Player.TextChannel.SendMessageAsync($"{arg.Reason} -> **{arg.Track.Title}**" + Environment.NewLine +
                                $"Сейчас играет: **{player.Track.Title}** <{player.Track.Url}>");
                    }
                    else if (player.Track == null && player.Queue.Count >= 1)// Если очередь не скипали, а она сама продвигается
                    {
                        if (!player.Queue.TryDequeue(out var queueable))
                        {
                            await arg.Player.TextChannel.SendMessageAsync($"{arg.Reason} -> {arg.Track.Title} и это конец очереди");
                            _ = InitiateDisconnectAsync(arg.Player, TimeSpan.FromSeconds(timeout));
                            return;
                        }

                        if (!(queueable is LavaTrack track))
                        {
                            await player.TextChannel.SendMessageAsync("Зачем мне это подсунули?)).");
                            _ = InitiateDisconnectAsync(arg.Player, TimeSpan.FromSeconds(timeout));
                            return;
                        }

                        await CancelDisconnect(arg);
                        await arg.Player.PlayAsync(track);
                        await arg.Player.TextChannel.SendMessageAsync($"{arg.Reason} -> **{arg.Track.Title}**" + Environment.NewLine +
                            $"Сейчас играет: **{track.Title}** <{track.Url}>");

                        return;
                    }
                    else
                    {
                        // Если в очереди больше нет треков
                        await arg.Player.TextChannel.SendMessageAsync($"{arg.Reason} -> {arg.Track.Title} и это конец очереди");
                        _ = InitiateDisconnectAsync(arg.Player, TimeSpan.FromSeconds(timeout));
                    }
                    serverFailCount[guild] = 0;
                    return;
                }
                else
                {
                    await arg.Player.TextChannel.SendMessageAsync($"**{arg.Track.Title}** вызвала ошибку {arg.Reason} (уже {serverFailCount[guild]} раз) и я добавил ее опять");
                    await arg.Player.PlayAsync(arg.Track);
                    return;
                }
            }

            // Если в очереди есть следующий трек, то просто пишем что он есть. Это если кто-то скипнул очередь, Skip переключает сам на следующший трек
            if (player.Track != null)
            {
                await CancelDisconnect(arg);
                await arg.Player.TextChannel.SendMessageAsync($"{arg.Reason} -> **{arg.Track.Title}**" + Environment.NewLine +
                        $"Сейчас играет: **{player.Track.Title}** <{player.Track.Url}>");
                return;
            }
            else if (player.Track == null && player.Queue.Count >= 1)// Если очередь не скипали, а она сама продвигается
            {
                if (!player.Queue.TryDequeue(out var queueable))
                {
                    await arg.Player.TextChannel.SendMessageAsync($"{arg.Reason} -> {arg.Track.Title} и это конец очереди");
                    _ = InitiateDisconnectAsync(arg.Player, TimeSpan.FromSeconds(timeout));
                    return;
                }

                if (!(queueable is LavaTrack track))
                {
                    await player.TextChannel.SendMessageAsync("Зачем мне это подсунули?)).");
                    _ = InitiateDisconnectAsync(arg.Player, TimeSpan.FromSeconds(timeout));
                    return;
                }

                await CancelDisconnect(arg);
                await arg.Player.PlayAsync(track);
                await arg.Player.TextChannel.SendMessageAsync($"{arg.Reason} -> **{arg.Track.Title}**" + Environment.NewLine +
                    $"Сейчас играет: **{track.Title}** <{track.Url}>");

                return;
            }
            else
            {
                await arg.Player.TextChannel.SendMessageAsync($"{arg.Reason} -> {arg.Track.Title} и это конец очереди");
                _ = InitiateDisconnectAsync(arg.Player, TimeSpan.FromSeconds(timeout));
                return;
            }


        }


        private async Task CancelDisconnect(TrackEndedEventArgs arg)
        {
            if (_disconnectTokens.ContainsKey(arg.Player.VoiceChannel.Id))
            {
                _disconnectTokens.TryGetValue(arg.Player.VoiceChannel.Id, out var value);

                if (value.IsCancellationRequested)
                    return;

                value.Cancel(true);
                //await arg.Player.TextChannel.SendMessageAsync("Оу май, мы продолжаем играть!!!"); 

            }
        }


        private async Task CancelDisconnect(TrackStartEventArgs arg)
        {
            if (_disconnectTokens.ContainsKey(arg.Player.VoiceChannel.Id))
            {
                _disconnectTokens.TryGetValue(arg.Player.VoiceChannel.Id, out var value);
                if (value.IsCancellationRequested)
                    return;

                value.Cancel(true);
            }
        }


        private async Task _lavaNode_OnTrackStarted(TrackStartEventArgs arg)
        {
            //await arg.Player.TextChannel.SendMessageAsync("Че началось то");
            await CancelDisconnect(arg);
        }


        private async Task InitiateDisconnectAsync(LavaPlayer player, TimeSpan timeSpan)
        {
            if (!_disconnectTokens.TryGetValue(player.VoiceChannel.Id, out var value))
            {
                value = new CancellationTokenSource();
                _disconnectTokens.TryAdd(player.VoiceChannel.Id, value);
            }
            else if (value.IsCancellationRequested)
            {
                _disconnectTokens.TryUpdate(player.VoiceChannel.Id, new CancellationTokenSource(), value);
                value = _disconnectTokens[player.VoiceChannel.Id];
            }
            var isCancelled = SpinWait.SpinUntil(() => value.IsCancellationRequested, timeSpan);
            if (isCancelled || player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                return;
            }
            await _lavaNode.LeaveAsync(player.VoiceChannel);
            await player.TextChannel.SendMessageAsync("Я устал молчать, я ухожу");
        }
    }
}

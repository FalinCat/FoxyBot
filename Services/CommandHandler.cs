using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Victoria;
using Victoria.EventArgs;

namespace FoxyBot.Services
{
    public class CommandHandler : InitializedService
    {
        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _service;
        private readonly IConfiguration _configuration;
        private readonly LavaNode _lavaNode;

        public CommandHandler(IServiceProvider provider, DiscordSocketClient client, CommandService service, IConfiguration configuration, LavaNode lavaNode)
        {
            _provider = provider;
            _client = client;
            _service = service;
            _configuration = configuration;
            _lavaNode = lavaNode;
            
        }

        private async Task Client_Ready()
        {
            if (!_lavaNode.IsConnected)
            {
                await _lavaNode.ConnectAsync();
            }
        }

        private async Task OnTrackEnded(TrackEndedEventArgs args)
        {



        }

        public override async  Task InitializeAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += OnMessageReceived;
            _client.Ready += Client_Ready;
            _lavaNode.OnTrackEnded += _lavaNode_OnTrackEnded;
            await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);

        }

        private async Task _lavaNode_OnTrackEnded(TrackEndedEventArgs arg)
        {
            var player = arg.Player;
            if (!player.Queue.TryDequeue(out var queueable))
            {
                await player.TextChannel.SendMessageAsync("В очереди не осталось треков");
                return;
            }

            if (!(queueable is LavaTrack track))
            {
                await player.TextChannel.SendMessageAsync("Как то так произошло, что следующий трек в очереди - не трек");
                return;
            }

            await arg.Player.PlayAsync(track);
            await arg.Player.TextChannel.SendMessageAsync(
                $"{arg.Reason}: {arg.Track.Title}\nСейчас играет: {track.Title}");
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
    }
}

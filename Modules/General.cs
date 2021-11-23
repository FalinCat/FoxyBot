using Discord;
using Discord.Audio;
using Discord.Commands;
using FoxyBot.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;

namespace FoxyBot.Modules
{
    public class General : ModuleBase<SocketCommandContext>
    {
       /*Player player = Player.getInstance();

        [Command("q")]
        public async Task PingAsync()
        {
            await Context.Channel.SendMessageAsync(player.GetQueueList());
        }

        [Command("play", RunMode = RunMode.Async)]
        public async Task PlayAsync(string song, IVoiceChannel channel = null)
        {
            channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("Зайди в голосовой канал"); return; }

            player.AddToQueue(song, channel, Context);
        }

        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            player.StopPlaying();

        }

        [Command("pause", RunMode = RunMode.Async)]
        public async Task PauseAsync()
        {
            player.PausePlaying();

        }*/
    }
}

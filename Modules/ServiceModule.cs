using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using System.Text;
using Victoria;

namespace FoxyBot.Modules
{
    public class ServiceModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        public ServiceModule(LavaNode lavaNode)
        {
            _lavaNode = lavaNode;
        }



        [Command("lava", RunMode = RunMode.Async)]
        public async Task ListAllAlailableServers()
        {
            var json = File.ReadAllText("servers.json");
            List<LavaServer> serverList = JsonConvert.DeserializeObject<List<LavaServer>>(json);

            var sb = new StringBuilder();
            sb.AppendLine("Список доступных LavaLink серверов:");
            for (int i = 0; i < serverList.Count; i++)
            {
                sb.AppendLine( i + " - **" + serverList[i].Host + (serverList[i].Host == Program.currentHost ? "**  <--- текущий сервер" : "**"));
            }

            await ReplyAsync(sb.ToString());
        }


        [Command("lava", RunMode = RunMode.Async)]
        public async Task ChangeLavaServerAsync([Remainder] string n)
        {
            if (ushort.TryParse(n, out var number))
            {
                var voiceState = Context.User as IVoiceState;
                if (voiceState?.VoiceChannel == null)
                {
                    await ReplyAsync("Необходимо находиться в голосовом канале." + Environment.NewLine + "Т.к. при перезапуске вся очередь очищается и воспросизведение останавливается. Во избежание Меддо-юза бота");
                    return;
                }

                _lavaNode.TryGetPlayer(Context.Guild, out var player);
                if (player != null && player?.VoiceChannel.Id != voiceState?.VoiceChannel.Id)
                {
                    await ReplyAsync("бот уже находится в голосовом канале: " + _lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name +
                                                ", а вы в канале - " + voiceState?.VoiceChannel + Environment.NewLine +
                                                "Т.к. при перезапуске вся очередь очищается и воспросизведение останавливается. Во избежание Меддо-юза бота");
                    return;
                }



                var json = File.ReadAllText("servers.json");
                List<LavaServer> serverList = JsonConvert.DeserializeObject<List<LavaServer>>(json);

                var server = serverList.ElementAtOrDefault(number);
                if (server == null)
                {
                    await ReplyAsync("Такого сервера не нашел");
                    return;
                }

                await ReplyAsync("Перезапускаю сервер... Новый адрес: **" + serverList[number].Host + "**");
                await Program.RestartHostWithNewLavaServer(number);
            }
            else
            {
                await ReplyAsync("Не распознал номер сервера как валидное число");
            }


            return;
        }

    }
}

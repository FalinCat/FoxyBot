using Discord.Commands;
using Newtonsoft.Json;
using System.Text;

namespace FoxyBot.Modules
{
    public class ServiceModule : ModuleBase<SocketCommandContext>
    {
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
        public async Task SearchAsyncCut([Remainder] string n)
        {
            if (ushort.TryParse(n, out var number))
            {
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

using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using FoxyBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Victoria;

namespace FoxyBot
{
    public class Program
    {
        public Queue<string> queue = new Queue<string>();
        static IHost? host;
        static List<LavaServer> serverList = new();
        public static string currentHost;

        public static async Task Main(string[] args)
        {
            var json = File.ReadAllText("servers.json");
            serverList = JsonConvert.DeserializeObject<List<LavaServer>>(json);
            host = BuildHost(2);
            currentHost = serverList[2].Host;

            using (host)
            {
                try
                {
                    await host.RunAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private static IHost BuildHost(int serverNumber)
        {
            var json = File.ReadAllText("servers.json");
            serverList = JsonConvert.DeserializeObject<List<LavaServer>>(json);

            var builder = new HostBuilder()
                .ConfigureAppConfiguration(x =>
                {
                    var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                    x.AddConfiguration(configuration);
                })
                .ConfigureLogging(x =>
                {
                    x.AddConsole();
                    x.SetMinimumLevel(LogLevel.Trace);
                })
                .ConfigureDiscordHost((context, config) =>
                {
                    config.SocketConfig = new DiscordSocketConfig
                    {
                        LogLevel = LogSeverity.Verbose,
                        AlwaysDownloadUsers = false,
                        MessageCacheSize = 200,
                        DefaultRetryMode = RetryMode.AlwaysRetry,
                    };

                    config.Token = context.Configuration["Token"];
                })
                .UseCommandService((context, config) =>
                {
                    config.CaseSensitiveCommands = false;
                    config.LogLevel = LogSeverity.Verbose;
                    config.DefaultRunMode = RunMode.Async;
                })
                .ConfigureServices((context, services) =>
                {
                    var lConf = new LavaConfig();
                    lConf.Hostname = serverList[serverNumber].Host;
                    lConf.Port = serverList[serverNumber].Port;
                    lConf.Authorization = serverList[serverNumber].Password;
                    lConf.IsSsl = serverList[serverNumber].Secure;

                    services
                    .AddHostedService<CommandHandler>()
                    .AddLavaNode(x =>
                    {
                        x.SelfDeaf = true;
                    })
                    .AddSingleton<LavaNode>()
                    .AddSingleton<LavaConfig>(lConf);
                })
                .UseConsoleLifetime();


            return builder.Build();
        }

        public static async Task RestartHostWithNewLavaServer(ushort serverNumber) {

            await host.StopAsync();
            host = BuildHost(serverNumber);

            currentHost = serverList[serverNumber].Host;

            using (host)
            {
                try
                {
                    await host.RunAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

        }


    }
}
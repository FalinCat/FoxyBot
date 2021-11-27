﻿using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using FoxyBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Victoria;

namespace FoxyBot
{ 
    public class Program
    {
        public Queue<string> queue = new Queue<string>();

        public static async Task Main(string[] args)
        {
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
                    //lConf.Hostname = "lavalink.devin-dev.xyz";
                    lConf.Hostname = "lava.devin-dev.xyz";
                    lConf.Port = 443;
                    lConf.Authorization = "lava123";
                    lConf.IsSsl = true;

                    //lConf.Hostname = "lava.link";
                    //lConf.Port = 80;
                    //lConf.Authorization = "somepass";
                    //lConf.ReconnectAttempts = 50;
                    //lConf.EnableResume = true;
                    //lConf.ReconnectDelay = TimeSpan.FromSeconds(5);
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


            var host = builder.Build();
            using (host)
            {
                await host.RunAsync();
            }

        }


    }
}
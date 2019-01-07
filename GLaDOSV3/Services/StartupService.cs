﻿using System;
using System.Data;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using GladosV3.Helpers;
using Microsoft.Extensions.Configuration;

namespace GladosV3.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;
        private BotSettingsHelper<string> _botSettingsHelper;
        // IServiceProvider, DiscordSocketClient, CommandService, and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(
            DiscordSocketClient discord,
            CommandService commands,
            IConfigurationRoot config,
            IServiceProvider provider,
            BotSettingsHelper<string> botSettingsHelper)
        {
            _config = config;
            _discord = discord;
            _commands = commands;
            _provider = provider;
            _botSettingsHelper = botSettingsHelper;
        }

        private Task<string> AskNotNull(string question)
        {
            Console.Write(question);
            string input = Console.ReadLine();
            while (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Enter something. This can't be empty.");
                Console.Write(question);
                input = Console.ReadLine();
            }
            return Task.FromResult(input);
        }
        public async Task FirstStartup()
        {
            using (DataTable dt = await SqLite.Connection.GetValuesAsync("BotSettings", "WHERE value IS NOT NULL"))
                if (dt.Rows.Count == 8)
                    return;
            //`prefix` TEXT, `name` INTEGER, `maintenance` TEXT, `ownerID` INTEGER, `co-owners` TEXT, `discord_game` TEXT, `discord_status` TEXT, `tokens_discord` TEXT"
            Console.WriteLine("Hello user! Looks like your starting this bot for the first time! You'll need to enter some values to start this bot.");
            Console.Write("Please enter your default bot prefix: ");
            string input = await AskNotNull("Please enter your default bot prefix: ");
            await SqLite.Connection.AddRecordAsync("BotSettings","name,value",new [] {"prefix", input});
            input = await AskNotNull("Perfect. Now please enter the name of the bot: ");
            await SqLite.Connection.AddRecordAsync("BotSettings", "name,value", new[] { "name", input });
            await SqLite.Connection.AddRecordAsync("BotSettings", "name,value", new[] { "maintenance", "" });
            input = await AskNotNull("Very good. Now add your user ID: ");
            await SqLite.Connection.AddRecordAsync("BotSettings", "name,value", new[] { "ownerID", input });
            Console.WriteLine("Now you'll can enter co-owners user IDs, this is totally optional (Press enter to skip).");
            Console.WriteLine("If you decide to add any, put them in format \"userID1,userID2\" without quotation marks.");
            Console.WriteLine("Now enter co-owners ID: ");
            input = Console.ReadLine();
            await SqLite.Connection.AddRecordAsync("BotSettings", "name,value", new[] { "co-owners", input });
            await SqLite.Connection.AddRecordAsync("BotSettings", "name,value", new[] { "discord_game", "" });
            await SqLite.Connection.AddRecordAsync("BotSettings", "name,value", new[] { "discord_status", "Online" });
            input = await AskNotNull("Ok! Now the final thing! Enter your bot token: ");
            await SqLite.Connection.AddRecordAsync("BotSettings", "name,value", new[] { "tokens_discord", input });
        }
        public async Task StartAsync()
        {
            SqLite.Start();
            await FirstStartup();
            Console.Title = _botSettingsHelper["name"];
            Console.Clear();
            Console.SetWindowSize(150, 35);
            Console.WriteLine("This bot is using a database to store it's settings. Add --resetdb to reset the configuration (token, owners, etc..).");
            string discordToken = _botSettingsHelper["tokens_discord"];     // Get the discord token from the config file
            string gameTitle = _botSettingsHelper["discord_game"]; // Get bot's game status
            await _discord.SetGameAsync(gameTitle); // set bot's game status
            try
            {
                await _discord.LoginAsync(TokenType.Bot, discordToken, true); // Login to discord
                await _discord.StartAsync(); // Connect to the websocket
            }
            catch (HttpException ex) // Some error checking
            {
                if (ex.DiscordCode == 401 || ex.HttpCode == HttpStatusCode.Unauthorized)
                    Tools.WriteColorLine(ConsoleColor.Red, "Wrong or invalid token.");
                else if (ex.DiscordCode == 502 || ex.HttpCode == HttpStatusCode.BadGateway)
                    Tools.WriteColorLine(ConsoleColor.Yellow, "Gateway unavailable.");
                else if (ex.DiscordCode == 400 || ex.HttpCode == HttpStatusCode.BadRequest)
                    Tools.WriteColorLine(ConsoleColor.Red, "Bad request. Please wait for an update.");
                Tools.WriteColorLine(ConsoleColor.Red, $"Discord has returned an error code: {ex.DiscordCode}{Environment.NewLine}Here's exception message: {ex.Message}");
                Task.Delay(10000).Wait();
                Environment.Exit(0);
            }

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);     // Load commands and modules into the command service
            await new ExtensionLoadingService(_discord, _commands, _config, _provider).Load().ConfigureAwait(false);
        }
    }
}

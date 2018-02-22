﻿using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Discord.Net;
using GladosV3.Helpers;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GladosV3.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;

        // IServiceProvider, DiscordSocketClient, CommandService, and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(
            DiscordSocketClient discord,
            CommandService commands,
            IConfigurationRoot config,
            IServiceProvider provider)
        {
            _config = config;
            _discord = discord;
            _commands = commands;
            _provider = provider;
        }


        public async Task StartAsync()
        {
            Console.Title = _config["name"];
            Console.Clear();
            Console.SetWindowSize(150, 35);
            string discordToken = _config["tokens:discord"];     // Get the discord token from the config file
            string gameTitle = _config["discord:game"]; // Get bot's game status
            if (string.IsNullOrWhiteSpace(discordToken) || string.IsNullOrEmpty(discordToken))
                throw new Exception("Please enter your bot's token into the `_configuration.json` file found in the applications root directory.");
            else if (!string.IsNullOrWhiteSpace(discordToken) || !string.IsNullOrEmpty(discordToken))
                await _discord.SetGameAsync(gameTitle); // set bot's game status
            try
            {
                await _discord.LoginAsync(TokenType.Bot, discordToken, true); // Login to discord
                await _discord.StartAsync(); // Connect to the websocket
            }
            catch (HttpException ex) // Some error checking
            {
                if (ex.DiscordCode == 401 || ex.HttpCode == HttpStatusCode.Unauthorized)
                    Helpers.Tools.WriteColorLine(ConsoleColor.Red, "Wrong or invalid token.");
                else if (ex.DiscordCode == 502 || ex.HttpCode == HttpStatusCode.BadGateway)
                    Helpers.Tools.WriteColorLine(ConsoleColor.Yellow, "Gateway unavailable.");
                else if (ex.DiscordCode == 400 || ex.HttpCode == HttpStatusCode.BadRequest)
                    Helpers.Tools.WriteColorLine(ConsoleColor.Red, "Bad request. Please wait for an update.");
                Helpers.Tools.WriteColorLine(ConsoleColor.Red,
                    $"Discord has returned an error code: {ex.DiscordCode}{Environment.NewLine}Here's exception message: {ex.Message}");
                Task.Delay(10000).Wait(); 
                Environment.Exit(0);
            }

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());     // Load commands and modules into the command service
            if (Directory.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Modules")))
                foreach (var file in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Modules"))) // Bad extension loading
                {
                    if (Path.GetExtension(file) != ".dll") continue; 
                    if (new System.IO.FileInfo(file).Length == 0) continue; // file is empty!
                    try
                    {
                        if(!IsValidCLRFile(file)) continue; // file is not .NET assembly
                        var asm = Assembly.LoadFile(file);
                        if(!IsValidExtension(asm)) continue; // every extension must have ModuleInfo class
                        await LoadExtension(asm).ConfigureAwait(false); // load the extension
                        await _commands.AddModulesAsync(asm); // add the extension's commands
                        var modules = asm.GetTypes().Where(type => type.IsClass && !type.IsSpecialName && type.IsPublic)
                            .Aggregate(string.Empty, (current, type) => current + type.Name + ", ");
                        await LoggingService.Log(LogSeverity.Verbose, "Module",
                            $"Loaded modules: {modules.Remove(modules.Length - 2)} from {Path.GetFileNameWithoutExtension(file)}");
                    }
                    catch(BadImageFormatException) { }
                }
            SqLite.Start();
        }
        private bool IsValidCLRFile(string file) // based on PE headers
        {
            bool? returnBool = null;
            uint[] dataDictionaryRVA = new uint[16];
            uint[] dataDictionarySize = new uint[16];
            Stream fs = new FileStream(file, FileMode.Open,FileAccess.Read);
            BinaryReader reader = new BinaryReader(fs);
            fs.Position = 0x3C;
            var peHeader = reader.ReadUInt32();
            fs.Position = peHeader;
            var peHeaderSignature = reader.ReadUInt32();
            ushort dataDictionaryStart = Convert.ToUInt16(Convert.ToUInt16(fs.Position) + 0x60);
            fs.Position = dataDictionaryStart;
            for (int i = 0; i < 15; i++)
            {
                dataDictionaryRVA[i] = reader.ReadUInt32();
                dataDictionarySize[i] = reader.ReadUInt32();
            }
            if(peHeaderSignature != 17744)
            { LoggingService.Log(LogSeverity.Error, "Module", $"{file} has non-valid PE header!"); returnBool = false; }
            if (dataDictionaryRVA[13] == 64 && returnBool == null)
            {
                LoggingService.Log(LogSeverity.Error,"Module",$"{file} is NOT a valid CLR file!!");
                returnBool = false;
            }
            else
                returnBool = true;
            fs.Close();
            return (bool) returnBool;
        }
        public bool IsValidExtension(Assembly asm)
        {
            try
            {
                if (!asm.GetTypes().Any(t => t.Namespace.Contains("GladosV3.Module"))) return false;
                if (!asm.GetTypes().Any(type => (type.IsClass && type.IsPublic && type.Name == "ModuleInfo"))) return false; //extension doesn't have ModuleInfo class
                Type asmType = asm.GetTypes().Where(type => type.IsClass && type.Name == "ModuleInfo").Distinct().First(); //create type
                if (asmType.GetInterfaces().Distinct().FirstOrDefault() != typeof(IGladosModule)) return false; // extension's moduleinfo is not extended
                ConstructorInfo asmConstructor = asmType.GetConstructor(Type.EmptyTypes); // get extension's constructor
                object classO = asmConstructor.Invoke(new object[] { }); // create object of class
                if (string.IsNullOrWhiteSpace(GetModuleInfo(asmType, classO, "Name").ToString())) return false; // class doesn't have Name string
                if (string.IsNullOrWhiteSpace(GetModuleInfo(asmType, classO, "Version").ToString())) return false; // class doesn't have Version string
                if (string.IsNullOrWhiteSpace(GetModuleInfo(asmType, classO, "Author").ToString())) return false; // class doesn't have Author string
            }
            catch
            { return false; }
            return true;
        }
        public Task LoadExtension(Assembly asm)
        {
            try
            {
                Type asmType = asm.GetTypes().Where(type => type.IsClass && type.Name == "ModuleInfo").Distinct().First(); //create type
                ConstructorInfo asmConstructor = asmType.GetConstructor(Type.EmptyTypes);  // get extension's constructor
                object magicClassObject = asmConstructor.Invoke(new object[] { }); // create object of class
                var memberInfo = asmType.GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.Public); //get OnLoad method
                if (memberInfo != null)  // does the extension have OnLoad?
                    ((MethodInfo)memberInfo).Invoke(magicClassObject, new object[] { _discord, _commands, _config, _provider });// invoke OnLoad method
            }
            catch (Exception ex)
            { return Task.FromException(ex); }
            return Task.CompletedTask;
        }
        public object GetModuleInfo(Type type,object classO,string info)
        {
            var memberInfo = type.GetMethod(info, BindingFlags.Instance | BindingFlags.Public);
            return ((MethodInfo)memberInfo).Invoke(classO, new object[] { });
        }
    }
}

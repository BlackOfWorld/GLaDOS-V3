﻿using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GladosV3.Helpers;

namespace GladosV3.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class CommandHiddenAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var config = Tools.GetConfigAsync().GetAwaiter().GetResult();
            switch (context.Client.TokenType)
            {
                case TokenType.Bot:
                    return Task.FromResult(IsOwner.CheckPermission(context).GetAwaiter().GetResult() ? PreconditionResult.FromError("hidden") : PreconditionResult.FromSuccess());
                /*case TokenType.User:
                    return Task.FromResult(context.User.Id != context.Client.CurrentUser.Id ? PreconditionResult.FromError("hidden") : PreconditionResult.FromSuccess());*/
                default: return Task.FromResult(PreconditionResult.FromError("hidden"));
            }
        }
    }
}

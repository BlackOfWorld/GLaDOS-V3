﻿using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GladosV3.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequireMFAAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.Guild.MfaLevel == MfaLevel.Enabled)
                return Task.FromResult(context.Client.CurrentUser.IsMfaEnabled != true
                    ? PreconditionResult.FromError("The owner of this bot has MFA disabled!")
                    : PreconditionResult.FromSuccess());
            else
             return  Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}

﻿using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace GladosV3.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireUserPermissionAttribute : PreconditionAttribute
    {
        public GuildPermission? GuildPermission { get; }
        public ChannelPermission? ChannelPermission { get; }

        /// <summary>
        /// Require that the user invoking the command has a specified GuildPermission
        /// </summary>
        /// <remarks>This precondition will always fail if the command is being invoked in a private channel.</remarks>
        /// <param name="permission">The GuildPermission that the user must have. Multiple permissions can be specified by ORing the permissions together.</param>
        public RequireUserPermissionAttribute(GuildPermission permission)
        {
            GuildPermission = permission;
            ChannelPermission = null;
        }
        /// <summary>
        /// Require that the user invoking the command has a specified ChannelPermission.
        /// </summary>
        /// <param name="permission">The ChannelPermission that the user must have. Multiple permissions can be specified by ORing the permissions together.</param>
        /// <example>
        /// <code language="c#">
        ///     [Command("permission")]
        ///     [RequireUserPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.ReadMessages)]
        ///     public async Task HasPermission()
        ///     {
        ///         await ReplyAsync("You can read messages and the message history!");
        ///     }
        /// </code>
        /// </example>
        public RequireUserPermissionAttribute(ChannelPermission permission)
        {
            ChannelPermission = permission;
            GuildPermission = null;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var guildUser = context.User as IGuildUser;
            var application = context.Client.GetApplicationInfoAsync();
            if (GuildPermission.HasValue)
            {
                if (guildUser == null)
                    return Task.FromResult(PreconditionResult.FromError("Command must be used in a guild channel"));
                if (!guildUser.GuildPermissions.Has(GuildPermission.Value) && IsOwner.CheckPermission(context).GetAwaiter().GetResult())
                    return Task.FromResult(PreconditionResult.FromError($"User requires guild permission {GuildPermission.Value}"));
            }

            if (!ChannelPermission.HasValue) return Task.FromResult(PreconditionResult.FromSuccess());
            ChannelPermissions perms;
            if (context.Channel is IGuildChannel guildChannel)
                perms = guildUser.GetPermissions(guildChannel);
            else
                perms = ChannelPermissions.All(context.Channel);

            if (!perms.Has(ChannelPermission.Value) && IsOwner.CheckPermission(context).GetAwaiter().GetResult())
                return Task.FromResult(PreconditionResult.FromError($"User requires channel permission {ChannelPermission.Value}"));

            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}

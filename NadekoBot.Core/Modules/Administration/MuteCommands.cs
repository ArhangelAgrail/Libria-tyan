using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using System;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;
using NadekoBot.Modules.Administration.Services;
using NadekoBot.Core.Common.TypeReaders.Models;
using SixLabors.ImageSharp;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class MuteCommands : NadekoSubmodule<MuteService>
        {
            private readonly IImageCache _images;
            private readonly GuildTimezoneService _tz;
            private readonly DbService _db;

            public MuteCommands(IDataCache data, GuildTimezoneService tz, DbService db)
            {
                _images = data.LocalImages;
                _tz = tz;
                _db = db;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [Priority(0)]
            public async Task SetMuteRole([Remainder] string name)
            {
                name = name.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return;

                await _service.SetMuteRoleAsync(Context.Guild.Id, name).ConfigureAwait(false);

                await ReplyConfirmLocalized("mute_role_set").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [Priority(1)]
            public Task SetMuteRole([Remainder] IRole role)
                => SetMuteRole(role.Name);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            [Priority(0)]
            public async Task Mute(IGuildUser user)
            {
                try
                {
                    await _service.MuteUser(user, Context.User).ConfigureAwait(false);
                    await ReplyConfirmLocalized("user_muted", Format.Bold(user.ToString())).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalized("mute_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            [Priority(1)]
            public async Task Mute(IGuildUser user, StoopidTime time, [Remainder] string reason = null)
            {
                var tm = DateTime.UtcNow;
                tm = TimeZoneInfo.ConvertTime(tm, _tz.GetTimeZoneOrUtc(user.Guild.Id));
                var mod = _images.ImageUrls.Moderation;
                Uri imageToSend = mod.Mute[0];

                if (time.Time < TimeSpan.FromMinutes(1))
                    return;

                var log = new ModLog()
                {
                    UserId = user.Id,
                    GuildId = Context.Guild.Id,
                    Type = "Mute",
                    Reason = reason,
                    Moderator = Context.User.Id,
                };

                try
                {
                    await _service.TimedMute(user, Context.User, time.Time).ConfigureAwait(false);

                    using (var uow = _db.UnitOfWork)
                    {
                        uow.ModLog.Add(log);
                        uow.Complete();
                    }

                    try
                    {
                        await (await user.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder().WithErrorColor()
                                         .WithAuthor(GetText("muted_on", Context.Guild.ToString()))
                                         .WithThumbnailUrl(imageToSend.ToString())
                                         .WithFooter("ID: " + user.Id + " → " + $"[{tm:dd.MM.yyyy HH:mm:ss}]")
                                         .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.ToString()))
                                         .AddField(efb => efb.WithName(GetText("p_time")).WithValue(_service.GetTime(time.Time)).WithIsInline(true))
                                         .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-")))
                            .ConfigureAwait(false);
                    }
                    catch
                    {

                    }

                    var embed = new EmbedBuilder().WithErrorColor()
                        .WithFooter("ID: " + user.Id + " → " + $"[{tm:dd.MM.yyyy HH:mm:ss}]")
                        .WithThumbnailUrl(imageToSend.ToString())
                        .WithAuthor(name: user.ToString() + GetText("mute"), iconUrl: user.GetAvatarUrl())
                        .AddField(efb => efb.WithName(GetText("user")).WithValue(user.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("p_time")).WithValue(_service.GetTime(time.Time)).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-").WithIsInline(true));

                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    //await ReplyConfirmLocalized("user_muted_time", Format.Bold(user.ToString()), (int)time.Time.TotalMinutes).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                    await ReplyErrorLocalized("mute_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            public async Task Unmute(IGuildUser user)
            {
                try
                {
                    await _service.UnmuteUser(user.GuildId, user.Id, Context.User).ConfigureAwait(false);
                    await ReplyConfirmLocalized("user_unmuted", Format.Bold(user.ToString())).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalized("mute_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            public async Task ChatMute(IGuildUser user)
            {
                try
                {
                    await _service.MuteUser(user, Context.User, MuteType.Chat).ConfigureAwait(false);
                    await ReplyConfirmLocalized("user_chat_mute", Format.Bold(user.ToString())).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalized("mute_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            public async Task ChatUnmute(IGuildUser user)
            {
                try
                {
                    await _service.UnmuteUser(user.Guild.Id, user.Id, Context.User, MuteType.Chat).ConfigureAwait(false);
                    await ReplyConfirmLocalized("user_chat_unmute", Format.Bold(user.ToString())).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalized("mute_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            public async Task VoiceMute([Remainder] IGuildUser user)
            {
                try
                {
                    await _service.MuteUser(user, Context.User, MuteType.Voice).ConfigureAwait(false);
                    await ReplyConfirmLocalized("user_voice_mute", Format.Bold(user.ToString())).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalized("mute_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            public async Task VoiceUnmute([Remainder] IGuildUser user)
            {
                try
                {
                    await _service.UnmuteUser(user.GuildId, user.Id, Context.User, MuteType.Voice).ConfigureAwait(false);
                    await ReplyConfirmLocalized("user_voice_unmute", Format.Bold(user.ToString())).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalized("mute_error").ConfigureAwait(false);
                }
            }
        }
    }
}

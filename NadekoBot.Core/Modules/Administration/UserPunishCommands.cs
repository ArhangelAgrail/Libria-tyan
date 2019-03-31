using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Extensions;
using NadekoBot.Core.Services.Database.Models;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;
using NadekoBot.Modules.Administration.Services;
using NadekoBot.Core.Common.TypeReaders.Models;
using System;
using SixLabors.ImageSharp;
using NadekoBot.Core.Services;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class UserPunishCommands : NadekoSubmodule<UserPunishService>
        {
            private readonly IImageCache _images;
            private readonly MuteService _mute;
            private readonly GuildTimezoneService _tz;
            private readonly DbService _db;

            public UserPunishCommands(IDataCache data, MuteService mute, GuildTimezoneService tz, DbService db)
            {
                _images = data.LocalImages;
                _mute = mute;
                _tz = tz;
                _db = db;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            public async Task Warn(IGuildUser user, [Remainder] string reason = null)
            {
                var time = DateTime.UtcNow;
                time = TimeZoneInfo.ConvertTime(time, _tz.GetTimeZoneOrUtc(user.Guild.Id));
                var mod = _images.ImageUrls.Moderation;
                Uri imageToSend = mod.Warn[0];

                if (Context.User.Id != user.Guild.OwnerId
                    && (user.GetRoles().Select(r => r.Position).Max() >= ((IGuildUser)Context.User).GetRoles().Select(r => r.Position).Max()))
                {
                    await ReplyErrorLocalized("hierarchy").ConfigureAwait(false);
                    return;
                }

                WarningPunishment punishment;
                try
                {
                    punishment = await _service.Warn(Context.Guild, user.Id, Context.User, reason).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Warn(ex.Message);
                    await ReplyErrorLocalized("cant_apply_punishment").ConfigureAwait(false);
                    return;
                }

                if (punishment == null)
                {
                    var embed = new EmbedBuilder().WithErrorColor()
                        .WithFooter("ID: " + user.Id + " → " + $"[{time:dd.MM.yyyy HH:mm:ss}]")
                        .WithThumbnailUrl(imageToSend.ToString())
                        .WithAuthor(name: user.ToString() + GetText("warn"), iconUrl: user.GetAvatarUrl())
                        .AddField(efb => efb.WithName(GetText("user")).WithValue(user.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-").WithIsInline(true));

                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    //ReplyConfirmLocalized("user_warned", Format.Bold(user.ToString())).ConfigureAwait(false);

                    try
                    {
                        await (await user.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder().WithErrorColor()
                                     .WithAuthor(GetText("warned_on", Context.Guild.ToString()))
                                     .WithThumbnailUrl(imageToSend.ToString())
                                     .WithFooter("ID: " + user.Id + " → " + $"[{time:dd.MM.yyyy HH:mm:ss}]")
                                     .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.ToString()))
                                     .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-")))
                        .ConfigureAwait(false);
                    }
                    catch { }
                }

                else
                {
                    var embed = new EmbedBuilder().WithErrorColor()
                        .WithFooter("ID: " + user.Id + " → " + $"[{time:dd.MM.yyyy HH:mm:ss}]")
                        .WithThumbnailUrl(imageToSend.ToString())
                        .WithAuthor(name: user.ToString() + GetText("warn_and_punish") + GetText(punishment.Punishment.ToString()), iconUrl: user.GetAvatarUrl())
                        .AddField(efb => efb.WithName(GetText("user")).WithValue(user.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("p_time")).WithValue(_service.GetTime(punishment.Time / 60)).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-").WithIsInline(true));

                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    //ReplyConfirmLocalized("user_warned_and_punished", Format.Bold(user.ToString()), Format.Bold(punishment.ToString())).ConfigureAwait(false);

                    try
                    {
                        await (await user.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder().WithErrorColor()
                                     .WithAuthor(GetText("warned_on", Context.Guild.ToString()))
                                     .WithTitle(GetText("warn_and_punish_DM", GetText(punishment.Punishment.ToString()), punishment.Count))
                                     .WithThumbnailUrl(imageToSend.ToString())
                                     .WithFooter("ID: " + user.Id + " → " + $"[{time:dd.MM.yyyy HH:mm:ss}]")
                                     .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.ToString()).WithIsInline(true))
                                     .AddField(efb => efb.WithName(GetText("p_time")).WithValue(_service.GetTime(punishment.Time / 60)).WithIsInline(true))
                                     .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-")))
                        .ConfigureAwait(false);
                    }
                    catch { }
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(2)]
            public Task Warnlog(int page, IGuildUser user)
                => Warnlog(page, user.Id);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(3)]
            public Task Warnlog(IGuildUser user = null)
            {
                if (user == null)
                    user = (IGuildUser)Context.User;
                return Context.User.Id == user.Id || ((IGuildUser)Context.User).GuildPermissions.BanMembers ? Warnlog(user.Id) : Task.CompletedTask;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(0)]
            public Task Warnlog(int page, ulong userId)
                => InternalWarnlog(userId, page - 1);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(1)]
            public Task Warnlog(ulong userId)
                => InternalWarnlog(userId, 0);

            private async Task InternalWarnlog(ulong userId, int page)
            {
                if (page < 0)
                    return;
                var warnings = _service.UserWarnings(Context.Guild.Id, userId);

                warnings = warnings.Skip(page * 9)
                    .Take(9)
                    .ToArray();

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("warnlog_for", (Context.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString()))
                    .WithFooter(efb => efb.WithText(GetText("page", page + 1)));

                if (!warnings.Any())
                {
                    embed.WithDescription(GetText("warnings_none"));
                }
                else
                {
                    var i = page * 9;
                    foreach (var w in warnings)
                    {
                        i++;

                        var value = "[" + w.Reason + "]\nДата: " + w.DateAdded.Value.ToString("dd.MM.yyy");
                        if (w.Forgiven)
                            value = Format.Strikethrough(value) + " " + GetText("warn_cleared_by", w.ForgivenBy);

                        embed.AddField(x => x
                            .WithName($"#`{i}` Модератор: " + w.Moderator)
                            .WithValue(value));
                    }
                }

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            public async Task WarnlogAll(int page = 1)
            {
                if (--page < 0)
                    return;
                var warnings = _service.WarnlogAll(Context.Guild.Id);

                await Context.SendPaginatedConfirmAsync(page, (curPage) =>
                {
                    var ws = warnings.Skip(curPage * 15)
                        .Take(15)
                        .ToArray()
                        .Select(x =>
                        {
                            var all = x.Count();
                            var forgiven = x.Count(y => y.Forgiven);
                            var total = all - forgiven;
                            var usr = ((SocketGuild)Context.Guild).GetUser(x.Key);
                            return (usr?.ToString() ?? x.Key.ToString()) + $" | {total} ({all} - {forgiven})";
                        });

                    return new EmbedBuilder()
                        .WithTitle(GetText("warnings_list"))
                        .WithDescription(string.Join("\n", ws));
                }, warnings.Length, 15).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            public Task Warnclear(IGuildUser user, int index = 0)
                => Warnclear(user.Id, index);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            public async Task Warnclear(ulong userId, int index = 0)
            {
                if (index < 0)
                    return;
                var success = await _service.WarnClearAsync(Context.Guild.Id, userId, index, Context.User.ToString());
                var userStr = Format.Bold((Context.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString());
                if (index == 0)
                {
                    await ReplyConfirmLocalized("warnings_cleared", userStr).ConfigureAwait(false);
                }
                else
                {
                    if (success)
                    {
                        await ReplyConfirmLocalized("warning_cleared", Format.Bold(index.ToString()), userStr)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await ReplyErrorLocalized("warning_clear_fail").ConfigureAwait(false);
                    }
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            public async Task WarnPunish(int number, PunishmentAction punish, StoopidTime time = null)
            {
                var success = _service.WarnPunish(Context.Guild.Id, number, punish, time);

                if (!success)
                    return;

                await ReplyConfirmLocalized("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            public async Task WarnPunish(int number)
            {
                if (!_service.WarnPunish(Context.Guild.Id, number))
                {
                    return;
                }

                await ReplyConfirmLocalized("warn_punish_rem",
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task WarnPunishList()
            {
                var ps = _service.WarnPunishList(Context.Guild.Id);

                string list;
                if (ps.Any())
                {
                    list = string.Join("\n", ps.Select(x => $"{x.Count} -> {x.Punishment} {(x.Time <= 0 ? "" : x.Time.ToString() + "m")} "));
                }
                else
                {
                    list = GetText("warnpl_none");
                }
                await Context.Channel.SendConfirmAsync(
                    GetText("warn_punish_list"),
                    list).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            [Priority(1)]
            public async Task Ban(IGuildUser user, StoopidTime time, [Remainder] string msg = null)
            {
                var tm = DateTime.UtcNow;
                tm = TimeZoneInfo.ConvertTime(tm, _tz.GetTimeZoneOrUtc(user.Guild.Id));
                var mod = _images.ImageUrls.Moderation;
                Uri imageToSend = mod.Ban[0];

                if (time.Time > TimeSpan.FromDays(49))
                    return;
                if (Context.User.Id != user.Guild.OwnerId && (user.GetRoles().Select(r => r.Position).Max() >= ((IGuildUser)Context.User).GetRoles().Select(r => r.Position).Max()))
                {
                    await ReplyErrorLocalized("hierarchy").ConfigureAwait(false);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    try
                    {
                        await (await user.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder().WithErrorColor()
                                     .WithAuthor(GetText("baned_on", Context.Guild.ToString()))
                                     .WithThumbnailUrl(imageToSend.ToString())
                                     .WithFooter("ID: " + user.Id + " → " + $"[{tm:dd.MM.yyyy HH:mm:ss}]")
                                     .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.ToString()))
                                     .AddField(efb => efb.WithName(GetText("p_time")).WithValue(_service.GetTime(time.Time.TotalHours)).WithIsInline(true))
                                     .AddField(efb => efb.WithName(GetText("reason")).WithValue(msg ?? "-")))
                        .ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                var log = new ModLog()
                {
                    UserId = user.Id,
                    GuildId = Context.Guild.Id,
                    Type = "Ban",
                    Reason = msg,
                    Moderator = Context.User.Id,
                };

                await _mute.TimedBan(user, time.Time, Context.User.ToString() + " | " + msg).ConfigureAwait(false);

                using (var uow = _db.UnitOfWork)
                {
                    uow.ModLog.Add(log);
                    uow.Complete();
                }

                var embed = new EmbedBuilder().WithErrorColor()
                        .WithFooter("ID: " + user.Id + " → " + $"[{tm:dd.MM.yyyy HH:mm:ss}]")
                        .WithThumbnailUrl(imageToSend.ToString())
                        .WithAuthor(name: user.ToString() + GetText("ban"), iconUrl: user.GetAvatarUrl())
                        .AddField(efb => efb.WithName(GetText("user")).WithValue(user.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("p_time")).WithValue(_service.GetTime(time.Time.TotalHours)).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(msg ?? "-").WithIsInline(true));

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            [Priority(0)]
            public async Task Ban(IGuildUser user, [Remainder] string msg = null)
            {
                var tm = DateTime.UtcNow;
                tm = TimeZoneInfo.ConvertTime(tm, _tz.GetTimeZoneOrUtc(user.Guild.Id));
                var mod = _images.ImageUrls.Moderation;
                Uri imageToSend = mod.Ban[0];

                if (Context.User.Id != user.Guild.OwnerId && (user.GetRoles().Select(r => r.Position).Max() >= ((IGuildUser)Context.User).GetRoles().Select(r => r.Position).Max()))
                {
                    await ReplyErrorLocalized("hierarchy").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(msg))
                {
                    try
                    {
                        await (await user.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder().WithErrorColor()
                                     .WithAuthor(GetText("baned_on", Context.Guild.ToString()))
                                     .WithThumbnailUrl(imageToSend.ToString())
                                     .WithFooter("ID: " + user.Id + " → " + $"[{tm:dd.MM.yyyy HH:mm:ss}]")
                                     .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.ToString()))
                                     .AddField(efb => efb.WithName(GetText("reason")).WithValue(msg ?? "-")))
                        .ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                var log = new ModLog()
                {
                    UserId = user.Id,
                    GuildId = Context.Guild.Id,
                    Type = "Ban",
                    Reason = msg,
                    Moderator = Context.User.Id,
                };

                await Context.Guild.AddBanAsync(user, 7, Context.User.ToString() + " | " + msg).ConfigureAwait(false);

                using (var uow = _db.UnitOfWork)
                {
                    uow.ModLog.Add(log);
                    uow.Complete();
                }

                var embed = new EmbedBuilder().WithErrorColor()
                        .WithFooter("ID: " + user.Id + " → " + $"[{tm:dd.MM.yyyy HH:mm:ss}]")
                        .WithThumbnailUrl(imageToSend.ToString())
                        .WithAuthor(name: user.ToString() + GetText("ban"), iconUrl: user.GetAvatarUrl())
                        .AddField(efb => efb.WithName(GetText("user")).WithValue(user.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(msg ?? "-").WithIsInline(true));

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            public async Task Unban([Remainder]string user)
            {
                var bans = await Context.Guild.GetBansAsync().ConfigureAwait(false);

                var bun = bans.FirstOrDefault(x => x.User.ToString().ToLowerInvariant() == user.ToLowerInvariant());

                if (bun == null)
                {
                    await ReplyErrorLocalized("user_not_found").ConfigureAwait(false);
                    return;
                }

                await UnbanInternal(bun.User).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            public async Task Unban(ulong userId)
            {
                var bans = await Context.Guild.GetBansAsync().ConfigureAwait(false);

                var bun = bans.FirstOrDefault(x => x.User.Id == userId);

                if (bun == null)
                {
                    await ReplyErrorLocalized("user_not_found").ConfigureAwait(false);
                    return;
                }

                await UnbanInternal(bun.User).ConfigureAwait(false);
            }

            private async Task UnbanInternal(IUser user)
            {
                await Context.Guild.RemoveBanAsync(user).ConfigureAwait(false);

                await ReplyConfirmLocalized("unbanned_user", Format.Bold(user.ToString())).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.KickMembers)]
            [RequireUserPermission(GuildPermission.ManageMessages)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            public async Task Softban(IGuildUser user, [Remainder] string msg = null)
            {
                if (Context.User.Id != user.Guild.OwnerId && user.GetRoles().Select(r => r.Position).Max() >= ((IGuildUser)Context.User).GetRoles().Select(r => r.Position).Max())
                {
                    await ReplyErrorLocalized("hierarchy").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(msg))
                {
                    try
                    {
                        await user.SendErrorAsync(GetText("sbdm", Format.Bold(Context.Guild.Name), msg)).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                await Context.Guild.AddBanAsync(user, 7, Context.User.ToString() + " | " + msg).ConfigureAwait(false);

                try { await Context.Guild.RemoveBanAsync(user).ConfigureAwait(false); }
                catch { await Context.Guild.RemoveBanAsync(user).ConfigureAwait(false); }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("☣ " + GetText("sb_user"))
                        .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                        .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true)))
                    .ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.KickMembers)]
            [RequireBotPermission(GuildPermission.KickMembers)]
            public async Task Kick(IGuildUser user, [Remainder] string msg = null)
            {
                var time = DateTime.UtcNow;
                time = TimeZoneInfo.ConvertTime(time, _tz.GetTimeZoneOrUtc(user.Guild.Id));
                var mod = _images.ImageUrls.Moderation;
                Uri imageToSend = mod.Kick[0];

                if (Context.Message.Author.Id != user.Guild.OwnerId && user.GetRoles().Select(r => r.Position).Max() >= ((IGuildUser)Context.User).GetRoles().Select(r => r.Position).Max())
                {
                    await ReplyErrorLocalized("hierarchy").ConfigureAwait(false);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    try
                    {
                        await (await user.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder().WithErrorColor()
                                     .WithAuthor(GetText("kicked_on", Context.Guild.ToString()))
                                     .WithThumbnailUrl(imageToSend.ToString())
                                     .WithFooter("ID: " + user.Id + " → " + $"[{time:dd.MM.yyyy HH:mm:ss}]")
                                     .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.ToString()))
                                     .AddField(efb => efb.WithName(GetText("reason")).WithValue(msg ?? "-")))
                        .ConfigureAwait(false);
                    }
                    catch { }
                }

                await user.KickAsync(Context.User.ToString() + " | " + msg).ConfigureAwait(false);

                var log = new ModLog()
                {
                    UserId = user.Id,
                    GuildId = Context.Guild.Id,
                    Type = "Kick",
                    Reason = msg,
                    Moderator = Context.User.Id,
                };

                using (var uow = _db.UnitOfWork)
                {
                    uow.ModLog.Add(log);
                    uow.Complete();
                }

                var embed = new EmbedBuilder().WithErrorColor()
                        .WithFooter("ID: " + user.Id + " → " + $"[{time:dd.MM.yyyy HH:mm:ss}]")
                        .WithThumbnailUrl(imageToSend.ToString())
                        .WithAuthor(name: user.ToString() + GetText("kick"), iconUrl: user.GetAvatarUrl())
                        .AddField(efb => efb.WithName(GetText("user")).WithValue(user.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(Context.User.Mention).WithIsInline(true))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(msg ?? "-").WithIsInline(true));

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [RequireBotPermission(GuildPermission.BanMembers)]
            [OwnerOnly]
            public async Task MassKill([Remainder] string people)
            {
                if (string.IsNullOrWhiteSpace(people))
                    return;

                var (bans, missing) = _service.MassKill((SocketGuild)Context.Guild, people);

                var missStr = string.Join("\n", missing);
                if (string.IsNullOrWhiteSpace(missStr))
                    missStr = "-";

                //send a message but don't wait for it
                var banningMessageTask = Context.Channel.EmbedAsync(new EmbedBuilder()
                    .WithDescription(GetText("mass_kill_in_progress", bans.Count()))
                    .AddField(GetText("invalid", missing), missStr)
                    .WithOkColor());

                Bc.Reload();

                //do the banning
                await Task.WhenAll(bans
                    .Where(x => x.Id.HasValue)
                    .Select(x => Context.Guild.AddBanAsync(x.Id.Value, 7, x.Reason, new RequestOptions()
                    {
                        RetryMode = RetryMode.AlwaysRetry,
                    })))
                    .ConfigureAwait(false);

                //wait for the message and edit it
                var banningMessage = await banningMessageTask.ConfigureAwait(false);

                await banningMessage.ModifyAsync(x => x.Embed = new EmbedBuilder()
                    .WithDescription(GetText("mass_kill_completed", bans.Count()))
                    .AddField(GetText("invalid", missing), missStr)
                    .WithOkColor()
                    .Build()).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(2)]
            public Task Modlog(int page, IGuildUser user)
                => Modlog(page, user.Id);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [Priority(3)]
            public Task Modlog(IGuildUser user = null)
            {
                if (user == null)
                    user = (IGuildUser)Context.User;
                return ((IGuildUser)Context.User).GuildPermissions.BanMembers ? Modlog(user.Id) : Task.CompletedTask;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(0)]
            public Task Modlog(int page, ulong userId)
                => InternalModlog(userId, page - 1);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(1)]
            public Task Modlog(ulong userId)
                => InternalModlog(userId, 0);

            private async Task InternalModlog(ulong userId, int page)
            {
                if (page < 0)
                    return;
                var modlogs = _service.UserModLogs(Context.Guild.Id, userId);

                modlogs = modlogs.Skip(page * 9)
                    .Take(9)
                    .ToArray();

                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor(name: (Context.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString(), iconUrl: (Context.Guild as SocketGuild)?.GetUser(userId).GetAvatarUrl())
                    .WithTitle(GetText("modlog_for"))
                    .WithFooter(efb => efb.WithText(GetText("page", page + 1)));

                if (!modlogs.Any())
                {
                    embed.WithDescription(GetText("modlogs_none"));
                }
                else
                {
                    var i = page * 9;
                    foreach (var w in modlogs)
                    {
                        i++;
                        var value = "**" + GetText(w.Type) + ":** " + "[" + w.Reason + "]\nДата: " + w.DateAdded.Value.ToString("dd.MM.yyy");

                        embed.AddField(x => x
                            .WithName($"#`{i}` Модератор: {(Context.Guild as SocketGuild)?.GetUser(w.Moderator)?.ToString() ?? w.Moderator.ToString()}")
                            .WithValue(value));
                    }
                }

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(3)]
            public async Task ModStats([Remainder] IUser user)
            {
                var embed = new EmbedBuilder().WithTitle(GetText("modstats"))
                    .WithOkColor();

                var stats = _service.ModeratorStats(Context.Guild.Id, user.Id);
                if (stats.Length > 0)
                {
                    embed.AddField(GetText("moderator"), $"__**{user.ToString().TrimTo(20)}**__", true)
                    .AddField(GetText("full"), $"**{stats.Count(x => x.Type == "Warn")}** - Warn\n**{stats.Count(x => x.Type == "Mute")}** - Mute\n**{stats.Count(x => x.Type == "Ban")}** - Ban\n", true);

                    var last = stats.Where(x => x.DateAdded > DateTime.UtcNow.AddDays(-7));
                    embed.AddField(GetText("last"), $"**{last.Count(x => x.Type == "Warn")}** - Warn\n**{last.Count(x => x.Type == "Mute")}** - Mute\n**{last.Count(x => x.Type == "Ban")}** - Ban\n", true);
                }
                else embed.WithDescription(GetText("user_not_found"));

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(1)]
            public async Task ModStats(int page = 1)
            {
                if (page < 1)
                    return;

                var embed = new EmbedBuilder().WithTitle(GetText("modstats"))
                    .WithOkColor()
                    .WithFooter(GetText("page", page));

                var stats = _service.AllStats(Context.Guild.Id).Skip((page - 1) * 7).Take(7);

                if (stats.Count() == 0)
                    embed.WithDescription(GetText("warnpl_none"));
                else
                    foreach (var moders in stats)
                    {
                        embed.AddField(GetText("moderator"), $"__**```{_service.GetUserById(moders.Key).TrimTo(15, true)}```**__", true)
                            .AddField(GetText("full"), $"**{moders.Count(x => x.Type == "Warn")}** - Warn\n**{moders.Count(x => x.Type == "Mute")}** - Mute\n**{moders.Count(x => x.Type == "Ban")}** - Ban\n", true);

                        var last = moders.Where(x => x.DateAdded > DateTime.UtcNow.AddDays(-7));
                        embed.AddField(GetText("last"), $"**{last.Count(x => x.Type == "Warn")}** - Warn\n**{last.Count(x => x.Type == "Mute")}** - Mute\n**{last.Count(x => x.Type == "Ban")}** - Ban\n", true);
                    }

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.BanMembers)]
            [Priority(0)]
            public async Task ModStats([Remainder] IRole role = null)
            {
                role = role ?? Context.Guild.EveryoneRole;

                var members = (await role.GetMembersAsync().ConfigureAwait(false));
                var membersArray = members as IUser[] ?? members.ToArray();
                if (membersArray.Length == 0)
                {
                    return;
                }

                var embed = new EmbedBuilder().WithTitle(GetText("modstats"))
                    .WithOkColor();

                foreach (var moders in membersArray)
                {
                    var stats = _service.ModeratorStats(Context.Guild.Id, moders.Id);
                    if (stats.Length > 0)
                    {
                        embed.AddField(GetText("moderator"), $"__**```{moders.ToString().TrimTo(15, true)}```**__", true)
                        .AddField(GetText("full"), $"**{stats.Count(x => x.Type == "Warn")}** - Warn\n**{stats.Count(x => x.Type == "Mute")}** - Mute\n**{stats.Count(x => x.Type == "Ban")}** - Ban\n", true);

                        var last = stats.Where(x => x.DateAdded > DateTime.UtcNow.AddDays(-7));
                        embed.AddField(GetText("last"), $"**{last.Count(x => x.Type == "Warn")}** - Warn\n**{last.Count(x => x.Type == "Mute")}** - Mute\n**{last.Count(x => x.Type == "Ban")}** - Ban\n", true);
                    }
                }

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
        }
    }
}

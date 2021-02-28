using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Common.Attributes;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Extensions;
using NadekoBot.Modules.Xp.Common;
using NadekoBot.Modules.Xp.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;
using Octokit;

namespace NadekoBot.Modules.Xp
{
    public partial class Xp
    {
        [Group]
        public class Club : NadekoSubmodule<ClubService>
        {
            private readonly XpService _xps;

            public Club(XpService xps)
            {
                _xps = xps;
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubTransfer([Remainder]IUser newOwner)
            {
                var club = _service.TransferClub(Context.User, newOwner);

                if (club != null)
                    await ReplyConfirmLocalized("club_transfered",
                        Format.Bold(club.Name),
                        Format.Bold(newOwner.ToString())).ConfigureAwait(false);
                else
                    await ReplyErrorLocalized("club_transfer_failed").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubAdmin([Remainder]IUser toAdmin)
            {
                bool admin;
                try
                {
                    admin = _service.ToggleAdmin(Context.User, toAdmin);
                }
                catch (InvalidOperationException)
                {
                    await ReplyErrorLocalized("club_admin_error").ConfigureAwait(false);
                    return;
                }

                if (admin)
                    await ReplyConfirmLocalized("club_admin_add", Format.Bold(toAdmin.ToString())).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalized("club_admin_remove", Format.Bold(toAdmin.ToString())).ConfigureAwait(false);

            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubCreate([Remainder]string clubName)
            {
                if (string.IsNullOrWhiteSpace(clubName) || clubName.Length > 20)
                    return;

                if (!_service.CreateClub(Context.User, clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalized("club_create_error").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalized("club_created", Format.Bold(club.Name)).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubIcon([Remainder]string url = null)
            {
                if ((!Uri.IsWellFormedUriString(url, UriKind.Absolute) && url != null)
                    || !await _service.SetClubIcon(Context.User.Id, url == null ? null : new Uri(url)))
                {
                    await ReplyErrorLocalized("club_icon_error").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalized("club_icon_set").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public async Task ClubInformation(IUser user = null)
            {
                user = user ?? Context.User;
                var club = _service.GetClubByMember(user);
                if (club == null)
                {
                    await ReplyErrorLocalized("club_not_exists").ConfigureAwait(false);
                    return;
                }

                await ClubInformation(club.Name).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubInformation([Remainder]string clubName = null)
            {
                if (string.IsNullOrWhiteSpace(clubName))
                {
                    await ClubInformation(Context.User).ConfigureAwait(false);
                    return;
                }

                Bc.Reload();
                if (DateTime.Now.Month != Bc.BotConfig.ClubsReset.Month)
                {
                    _xps.ClubsXpReset(3);
                }

                if (!_service.GetClubByName(clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalized("club_not_exists").ConfigureAwait(false);
                    return;
                }

                var lvl = new LevelStats(club.Xp);
                var users = club.Users
                    .OrderByDescending(x =>
                    {
                        var l = x.TotalXp - x.ClubXp;
                        if (club.OwnerId == x.Id)
                            return int.MaxValue;
                        else if (x.IsClubAdmin)
                            return int.MaxValue / 2 + l;
                        else
                            return l;
                    });

                var maxAmount = 500000;
                var target = "storage_role";

                if (club.textId != 0)
                { maxAmount = 9999999; target = "storage_next"; }
                else
                if (club.XpImageUrl != "")
                { maxAmount = 5000000; target = "storage_channel"; }
                else
                if (club.roleId != 0)
                { maxAmount = 1000000; target = "storage_card"; }
                
                var progress = _service.GetStorageProgress(club.Currency, maxAmount);

                await Context.SendPaginatedConfirmAsync(0, (page) =>
                {
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"{club.Name}")
                        .WithUrl(club.XpImageUrl)
                        .WithDescription(GetText("level_x", lvl.Level) + $" ({String.Format("{0:#,0}", club.Xp)} xp)")
                        .AddField(GetText("description"), string.IsNullOrWhiteSpace(club.Description) ? "-" : club.Description, false)
                        .AddField(GetText("owner_and_role"), $" ▹<@{club.Owner.UserId}>\n" + (club.roleId != 0 ? $"▹<@&{club.roleId}>" : GetText("club_no_role")), true)
                        .AddField(GetText("storage") + GetText(target), $" **{String.Format("{0:#,0}", club.Currency)}/{String.Format("{0:#,0}", maxAmount)}** {Bc.BotConfig.CurrencySign}\n{progress}", true)
                        .AddField(GetText("members_all", club.Users.Count, club.Members), string.Join("\n", users
                            .Skip(page * 10)
                            .Take(10)
                            .Select(x =>
                            {
                                var l = new LevelStats(x.TotalXp);
                                var user = x as IUser;
                                var lvlStr = Format.Bold($" ⟪{String.Format("{0:#,0}", x.TotalXp - x.ClubXp)} xp⟫");
                                if (club.OwnerId == x.Id)
                                    return x.ToString() + "🌟" + lvlStr;
                                else if (x.IsClubAdmin)
                                    return x.ToString() + "⭐" + lvlStr;
                                return x.ToString() + lvlStr;
                            })), false);

                    if (Uri.IsWellFormedUriString(club.ImageUrl, UriKind.Absolute))
                        return embed.WithThumbnailUrl(club.ImageUrl);

                    return embed;
                }, club.Users.Count, 10).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubBans(int page = 1)
            {
                if (--page < 0)
                    return Task.CompletedTask;

                var club = _service.GetClubWithBansAndApplications(Context.User.Id);
                if (club == null)
                    return ReplyErrorLocalized("club_not_exists_owner");

                var bans = club
                    .Bans
                    .Select(x => x.User)
                    .ToArray();

                return Context.SendPaginatedConfirmAsync(page,
                    curPage =>
                    {
                        var toShow = string.Join("\n", bans
                            .Skip(page * 10)
                            .Take(10)
                            .Select(x => x.ToString()));

                        if (toShow == "")
                            toShow = GetText("club_bans_null");

                        return new EmbedBuilder()
                            .WithTitle(GetText("club_bans_for", club.Name))
                            .WithDescription(toShow)
                            .WithOkColor();

                    }, bans.Length, 10);
            }


            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubApps(int page = 1)
            {
                if (--page < 0)
                    return Task.CompletedTask;

                var club = _service.GetClubWithBansAndApplications(Context.User.Id);
                if (club == null)
                    return ReplyErrorLocalized("club_not_exists_owner");

                var apps = club
                    .Applicants
                    .Select(x => x.User)
                    .ToArray();

                return Context.SendPaginatedConfirmAsync(page,
                    curPage =>
                    {
                        var toShow = string.Join("\n", apps
                            .Skip(page * 10)
                            .Take(10)
                            .Select(x => x.ToString()));

                        if (toShow == "")
                            toShow = GetText("club_apps_null");

                        return new EmbedBuilder()
                            .WithTitle(GetText("club_apps_for", club.Name))
                            .WithDescription(toShow)
                            .WithOkColor();

                    }, apps.Length, 10);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubApply([Remainder]string clubName)
            {
                if (string.IsNullOrWhiteSpace(clubName))
                    return;

                if (!_service.GetClubByName(clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalized("club_not_exists").ConfigureAwait(false);
                    return;
                }

                if (_service.ApplyToClub(Context.User, club))
                {
                    try
                    {
                        var clubAdmins = club.Users.Select(x => x).Where(x => x.IsClubAdmin == true);
                        foreach (var admin in clubAdmins)
                        {
                            var target = (Context.Guild as SocketGuild)?.GetUser(admin.UserId);
                            await (await target.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder().WithOkColor()
                                                                     .WithAuthor(GetText("club_new_apply", club.Name))
                                                                     .WithFooter("ID: " + Context.User.Id)
                                                                     .WithDescription(GetText("club_new_applycant", Context.User.Username, Context.User.Discriminator, Context.User.Mention)))
                                                        .ConfigureAwait(false);
                        }
                    }
                    catch { }
                    await ReplyConfirmLocalized("club_applied", Format.Bold(club.Name)).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_apply_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubAccept(IUser user)
                => ClubAccept(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubAccept([Remainder]string userName)
            {
                var clb = _service.GetClubByMember(Context.User);

                if (clb == null)
                {
                    await ReplyErrorLocalized("club_null");
                    return;
                }

                if (clb.Users.Count >= clb.Members)
                {
                    await ReplyErrorLocalized("club_limit");
                    return;
                }   

                if (_service.AcceptApplication(Context.User.Id, userName, Context.Guild, out var discordUser))
                {
                    var du = Context.User as IGuildUser;
                    var gu = await du.Guild.GetUserAsync(discordUser.UserId);

                    try
                    {
                        await (await gu.GetOrCreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder().WithOkColor()
                                         .WithAuthor(GetText("club_accepted_DM", clb.Name))
                                         .WithDescription(GetText("club_accepted_DM_desc", clb.Name, Context.User.Username, Context.User.Discriminator, Context.User.Mention)))
                            .ConfigureAwait(false);
                    }
                    catch { };

                    if (clb.roleId != 0)
                    {
                        var role = du.Guild.Roles.FirstOrDefault(x => x.Id == clb.roleId);
                        await gu.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    
                    await ReplyConfirmLocalized("club_accepted", Format.Bold(discordUser.ToString())).ConfigureAwait(false);
                }
                else
                    await ReplyErrorLocalized("club_accept_error").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Clubleave()
            {
                var clb = _service.GetClubByMember(Context.User);

                if (_service.LeaveClub(Context.User, Context.Guild))
                {
                    if (clb.roleId != 0)
                    {
                        var du = Context.User as IGuildUser;
                        var gu = await du.Guild.GetUserAsync(Context.User.Id);
                        var role = du.Guild.Roles.FirstOrDefault(x => x.Id == clb.roleId);
                        await gu.RemoveRoleAsync(role).ConfigureAwait(false);
                    }
                    await ReplyConfirmLocalized("club_left").ConfigureAwait(false);
                }
                else
                    await ReplyErrorLocalized("club_not_in_club").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubKick([Remainder]IUser user)
                => ClubKick(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubKick([Remainder]string userName)
            {
                var clb = _service.GetClubByMember(Context.User);

                if (clb == null)
                {
                    await ReplyErrorLocalized("club_null");
                    return;
                }   

                var usr = clb.Users.FirstOrDefault(x => x.ToString().ToUpperInvariant() == userName.ToUpperInvariant());
                if (usr == null)
                {
                    await ReplyErrorLocalized("club_user_not_found");
                    return;
                }

                if (clb.OwnerId == usr.Id || (usr.IsClubAdmin && clb.Owner.UserId != Context.User.Id))
                {
                    await ReplyErrorLocalized("club_not_owner");
                    return;
                }

                if (_service.Kick(Context.User.Id, usr, Context.Guild, out var club))
                {
                    if (clb.roleId != 0)
                    {
                        var du = Context.User as IGuildUser;
                        var gu = await du.Guild.GetUserAsync(usr.UserId);
                        var role = du.Guild.Roles.FirstOrDefault(x => x.Id == clb.roleId);
                        try
                        {
                            await gu.RemoveRoleAsync(role).ConfigureAwait(false);
                        }
                        catch { };
                        
                    }
                    await ReplyConfirmLocalized("club_user_kick", Format.Bold(userName), Format.Bold(club.Name));
                }
                else
                    await ReplyErrorLocalized("club_user_kick_fail");
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubBan([Remainder]IUser user)
                => ClubBan(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubBan([Remainder]string userName)
            {
                var clb = _service.GetClubWithBansAndApplications(Context.User.Id);

                if (clb == null)
                {
                    await ReplyErrorLocalized("club_null");
                    return;
                }

                var usr = clb.Users.FirstOrDefault(x => x.ToString().ToUpperInvariant() == userName.ToUpperInvariant())
                    ?? clb.Applicants.FirstOrDefault(x => x.User.ToString().ToUpperInvariant() == userName.ToUpperInvariant())?.User;
                if (usr == null)
                {
                    await ReplyErrorLocalized("club_user_not_found");
                    return;
                }
                    
                if (clb.OwnerId == usr.Id || (usr.IsClubAdmin && clb.Owner.UserId != Context.User.Id))
                {
                    await ReplyErrorLocalized("club_not_owner");
                    return;
                }

                if (_service.Ban(Context.User.Id, usr, Context.Guild, out var club))
                {
                    if (clb.roleId != 0)
                    {
                        var du = Context.User as IGuildUser;
                        var gu = await du.Guild.GetUserAsync(usr.UserId);
                        var role = du.Guild.Roles.FirstOrDefault(x => x.Id == clb.roleId);
                        try
                        {
                            await gu.RemoveRoleAsync(role).ConfigureAwait(false);
                        }
                        catch { };
                    }
                    await ReplyConfirmLocalized("club_user_banned", Format.Bold(userName), Format.Bold(club.Name));
                }
                else
                    await ReplyErrorLocalized("club_user_ban_fail");
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubDecline([Remainder]IUser user)
                => ClubDecline(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubDecline([Remainder]string userName)
            {
                var clb = _service.GetClubWithBansAndApplications(Context.User.Id);

                if (clb == null)
                {
                    await ReplyErrorLocalized("club_null");
                    return;
                }

                var usr = clb.Applicants.First(x => x.User.ToString().ToUpperInvariant() == userName.ToUpperInvariant())?.User;
                if (usr == null)
                {
                    await ReplyErrorLocalized("club_user_not_found");
                    return;
                }

                if (clb.OwnerId == usr.Id || (usr.IsClubAdmin && clb.Owner.UserId != Context.User.Id))
                {
                    await ReplyErrorLocalized("club_not_owner");
                    return;
                }

                if (_service.Decline(Context.User.Id, usr, out var club))
                {
                    await ReplyConfirmLocalized("club_user_declined", Format.Bold(userName), Format.Bold(club.Name));
                }
                else
                    await ReplyErrorLocalized("club_user_decline_fail");
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubUnBan([Remainder]IUser user)
                => ClubUnBan(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public Task ClubUnBan([Remainder]string userName)
            {
                if (_service.UnBan(Context.User.Id, userName, out var club))
                    return ReplyConfirmLocalized("club_user_unbanned", Format.Bold(userName), Format.Bold(club.Name));
                else
                    return ReplyErrorLocalized("club_user_unban_fail");
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubLevelReq(int level)
            {
                if (_service.ChangeClubLevelReq(Context.User.Id, level))
                {
                    await ReplyConfirmLocalized("club_level_req_changed", Format.Bold(level.ToString())).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_level_req_change_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubDescription([Remainder] string desc = null)
            {
                if (_service.ChangeClubDescription(Context.User.Id, desc))
                {
                    await ReplyConfirmLocalized("club_desc_updated", Format.Bold(desc ?? "-")).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_desc_update_failed").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public async Task ClubDisband()
            {
                var embed = new EmbedBuilder()
                       .WithTitle(GetText("club_disband"))
                       .WithDescription(GetText("club_disband_confirm"));

                if (!await PromptUserConfirmAsync(embed).ConfigureAwait(false))
                    return;

                if (_service.Disband(Context.User.Id, Context.Guild, out ClubInfo club))
                {
                    if (club.roleId != 0)
                    {
                        var du = Context.User as IGuildUser;
                        var role = du.Guild.Roles.FirstOrDefault(x => x.Id == club.roleId);

                        await role.DeleteAsync().ConfigureAwait(false);
                    }
                    await ReplyConfirmLocalized("club_disbanded", Format.Bold(club.Name)).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_disband_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            [Priority(0)]
            public async Task ClubDisband([Remainder]string clubName = null)
            {
                var embed = new EmbedBuilder()
                       .WithTitle(GetText("club_disband"))
                       .WithDescription(GetText("club_disband_confirm"));

                if (!await PromptUserConfirmAsync(embed).ConfigureAwait(false))
                    return;

                if (!_service.GetClubByName(clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalized("club_not_exists").ConfigureAwait(false);
                    return;
                }

                if (_service.Disband(club, Context.Guild))
                {
                    if (club.roleId != 0)
                    {
                        var du = Context.User as IGuildUser;
                        var role = du.Guild.Roles.FirstOrDefault(x => x.Id == club.roleId);

                        await role.DeleteAsync().ConfigureAwait(false);
                    }

                    await ReplyConfirmLocalized("club_disbanded", Format.Bold(club.ToString())).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_disband_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ClubStorageAward(int amount, [Remainder]string clubName = null)
            {
                if (!_service.GetClubByName(clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalized("club_not_exists").ConfigureAwait(false);
                    return;
                }

                if (_service.StorageAward(amount, clubName))
                {
                    await ReplyConfirmLocalized("club_storage_awarded", amount, Format.Bold(clubName)).ConfigureAwait(false);
                }
                    
            }

            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubLeaderboard(int page = 1)
            {
                if (--page < 0 || page > 100)
                    return Task.CompletedTask;

                return Context.SendPaginatedConfirmAsync(page, (curPage) =>
                {
                    Bc.Reload();
                    if (DateTime.Now.Month != Bc.BotConfig.ClubsReset.Month)
                    {
                        _xps.ClubsXpReset(3);
                    }

                    var clubs = _service.GetClubLeaderboardPage(curPage);

                    var embed = new EmbedBuilder()
                        .WithTitle(GetText("club_leaderboard"))
                        .WithFooter(GetText("page", curPage + 1))
                        .WithOkColor();

                    if (!clubs.Any())
                        return embed.WithDescription("-");
                    else
                    {
                        var i = curPage * 10;
                        foreach (var club in clubs)
                        {
                            embed.AddField($"#{++i} " + club.Name + $" - [{GetText("club_users", club.Users.Count)}]", GetText("club_leaderboard_xp", new LevelStats(club.Xp).Level, club.Xp.ToString()), false);
                        }
                        return embed;
                    }
                }, 1000, 10, addPaginatedFooter: false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubRoleCreate(Rgba32 color = default)
            {
                var club = _service.GetClubByMember(Context.User);
                if (club == null)
                {
                    await ReplyErrorLocalized("club_null").ConfigureAwait(false);
                    return;
                }

                if (club.Owner.UserId != Context.User.Id)
                {
                    await ReplyErrorLocalized("club_not_owner").ConfigureAwait(false);
                    return;
                }

                if (club.roleId != 0)
                {
                    await ReplyErrorLocalized("club_role_exists").ConfigureAwait(false);
                    return;
                }
                    
                if (club.Currency < 500000)
                {
                    await ReplyErrorLocalized("club_not_enough").ConfigureAwait(false);
                    return;
                } 

                var role = await Context.Guild.CreateRoleAsync(club.Name + " club", color: new Color(color.R, color.G, color.B)).ConfigureAwait(false);

                if (await _service.RoleCreate(Context.User, role))
                    await ReplyConfirmLocalized("club_role_created").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubTextCreate(Rgba32 color = default)
            {
                var club = _service.GetClubByMember(Context.User);
                if (club == null)
                {
                    await ReplyErrorLocalized("club_null").ConfigureAwait(false);
                    return;
                }

                if (club.Owner.UserId != Context.User.Id)
                {
                    await ReplyErrorLocalized("club_not_owner").ConfigureAwait(false);
                    return;
                }

                if (club.roleId == 0)
                {
                    await ReplyErrorLocalized("club_role_not_exists").ConfigureAwait(false);
                    return;
                }

                if (club.XpImageUrl == "")
                {
                    await ReplyErrorLocalized("club_xp_image_not_exists").ConfigureAwait(false);
                    return;
                }

                if (club.textId != 0)
                {
                    await ReplyErrorLocalized("club_text_exists").ConfigureAwait(false);
                    return;
                }

                if (club.Currency < 5000000)
                {
                    await ReplyErrorLocalized("club_not_enough").ConfigureAwait(false);
                    return;
                }

                var overwriteAllow = new OverwritePermissions(PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Allow, 
                    PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, 
                    PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, 
                    PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit);
                var overwriteDeny = new OverwritePermissions(PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Deny,
                    PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit,
                    PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit,
                    PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit, PermValue.Inherit);

                var everyoneRole = Context.Guild.EveryoneRole;
                var clubRole = Context.Guild.GetRole(club.roleId);

                var text = await Context.Guild.CreateTextChannelAsync(club.Name + "_club", prop => prop.CategoryId = 436911139436232705);
                await text.AddPermissionOverwriteAsync(clubRole, overwriteAllow);
                await text.AddPermissionOverwriteAsync(everyoneRole, overwriteDeny);

                if (_service.TextCreate(Context.User, text))
                await ReplyConfirmLocalized("club_text_created").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubPlaceAdd(Rgba32 color = default)
            {
                var club = _service.GetClubByMember(Context.User);
                if (club == null)
                {
                    await ReplyErrorLocalized("club_null").ConfigureAwait(false);
                    return;
                }

                if (club.Owner.UserId != Context.User.Id)
                {
                    await ReplyErrorLocalized("club_not_owner").ConfigureAwait(false);
                    return;
                }

                if (club.Currency < 50000)
                {
                    await ReplyErrorLocalized("club_not_enough").ConfigureAwait(false);
                    return;
                }

                if (_service.PlaceAdd(Context.User))
                    await ReplyConfirmLocalized("club_place_added", club.Members + 1).ConfigureAwait(false);
            }


            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubInvestLb(int page = 1)
            {
                if (--page < 0 || page > 20)
                    return Task.CompletedTask;

                var club = _service.GetClubByMember(Context.User);
                if (club == null)
                {
                    return Task.CompletedTask;
                }

                return Context.SendPaginatedConfirmAsync(page, (curPage) =>
                {
                    var users = club.Users;

                    List<string> list = new List<string>();

                    list.AddRange(users.Select(x =>
                         {
                             var sum = _service.GetAmountByUser(x.UserId);
                             var sumStr = $"{sum}{Bc.BotConfig.CurrencySign} - {String.Format("{0:0.##}", (x.TotalXp - x.ClubXp) * 0.001)}% ({(int)((x.TotalXp - x.ClubXp) * 0.00001 * club.TotalCurrency)}{Bc.BotConfig.CurrencySign}) - <@{x.UserId}>";
                             return sumStr;
                         }));

                    var result = list.OrderByDescending(x => Convert.ToInt32(x.Split(Bc.BotConfig.CurrencySign).First()));
                    var total = club.Users.Select(x => x.ClubInvetsAmount).Sum();

                    var embed = new EmbedBuilder()
                        .WithAuthor(GetText("club_top_investers", club.Name))
                        .WithTitle(GetText("club_total_invests", total, Bc.BotConfig.CurrencySign) + "\n" + GetText("club_month_invests", club.TotalCurrency, Bc.BotConfig.CurrencySign))
                        .WithFooter(GetText("page", curPage + 1))
                        .WithOkColor()
                        .AddField(GetText("members", club.Users.Count), Format.Bold(string.Join("\n", result.Skip(curPage*10).Take(10))), false);
                    return embed;
                }, club.Users.Count, 10, addPaginatedFooter: false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubInvestLbReserve(int page = 1)
            {
                if (--page < 0 || page > 20)
                    return Task.CompletedTask;

                var club = _service.GetClubByMember(Context.User);
                if (club == null)
                {
                    return Task.CompletedTask;
                }

                return Context.SendPaginatedConfirmAsync(page, (curPage) =>
                {
                    var users = club.Users;

                    List<string> list = new List<string>();

                    list.AddRange(users.Select(x =>
                    {
                        var sum = _service.GetAmountByUser(x.UserId);
                        var sumStr = $"{sum}{Bc.BotConfig.CurrencySign} - {String.Format("{0:0.##}", (x.TotalXp - x.ClubXp) * 0.001)}% ({(int)((x.TotalXp - x.ClubXp) * 0.00001 * club.TotalCurrency)}{Bc.BotConfig.CurrencySign}) - {x.Username}";
                        return sumStr;
                    }));

                    var result = list.OrderByDescending(x => Convert.ToInt32(x.Split(Bc.BotConfig.CurrencySign).First()));
                    var total = club.Users.Select(x => x.ClubInvetsAmount).Sum();

                    var embed = new EmbedBuilder()
                        .WithAuthor(GetText("club_top_investers", club.Name))
                        .WithTitle(GetText("club_total_invests", String.Format("{0:#,0}", total), Bc.BotConfig.CurrencySign) + "\n" + GetText("club_month_invests", String.Format("{0:#,0}", club.TotalCurrency), Bc.BotConfig.CurrencySign))
                        .WithFooter(GetText("page", curPage + 1))
                        .WithOkColor()
                        .AddField(GetText("members", club.Users.Count), Format.Bold(string.Join("\n", result.Skip(curPage * 10).Take(10))), false);
                    return embed;
                }, club.Users.Count, 10, addPaginatedFooter: false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubInTest(int page = 1)
            {
                if (--page < 0 || page > 20)
                    return Task.CompletedTask;

                var club = _service.GetClubByMember(Context.User);
                if (club == null)
                {
                    return Task.CompletedTask;
                }

                return Context.SendPaginatedConfirmAsync(page, (curPage) =>
                {
                    var users = club.Users;

                    List<string> list = new List<string>();

                    list.AddRange(users.Select(x =>
                    {
                        var sum = _service.GetAmountByUser(x.UserId);
                        var sumStr = $"{sum}{Bc.BotConfig.CurrencySign} + {String.Format("{0:0.##}", (x.TotalXp - x.ClubXp) * 0.001)}% - <@{x.UserId}>";
                        return sumStr;
                    }));

                    var result = list.OrderByDescending(x => Convert.ToInt32(x.Split(Bc.BotConfig.CurrencySign).First()));

                    var embed = new EmbedBuilder()
                        .WithTitle(GetText("club_top_investers", club.Name))
                        .WithFooter(GetText("page", curPage + 1))
                        .WithOkColor()
                        .AddField(GetText("members", club.Users.Count), Format.Bold(string.Join("\n", result.Skip(curPage * 10).Take(10))), false);
                    return embed;
                }, club.Users.Count, 10, addPaginatedFooter: false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ClubInvestRestore([Remainder]string clubName = null)
            {
                if (!_service.GetClubByName(clubName, out ClubInfo club))
                {
                    return Task.CompletedTask;
                }

                return Context.SendPaginatedConfirmAsync(1, (curPage) =>
                {
                    var users = club.Users;

                    List<string> list = new List<string>();

                    list.AddRange(users.Select(x =>
                    {
                        var sum = Convert.ToInt32(_service.GetAmountByUserOld(x.UserId, club.Name)) * -1;
                        var sumin = _service.SetAmountByUser(x.UserId, sum);
                        return Convert.ToString(sumin); 
                    }));

                    var result = list.OrderByDescending(x => Convert.ToInt32(x.Split(":").First()));

                    var embed = new EmbedBuilder()
                            .WithTitle(GetText("club_top_investers"))
                            .WithFooter(GetText("page", 1))
                            .WithOkColor()
                            .AddField(GetText("members", club.Users.Count), Format.Bold(string.Join("\n", result.Skip(0).Take(10))), false);
                    return embed;
                }, club.Users.Count, 10, addPaginatedFooter: false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubXpImageCreate([Remainder]string url = null)
            {
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) && url != null)
                {
                    await ReplyErrorLocalized("club_image_error").ConfigureAwait(false);
                    return;
                }

                var club = _service.GetClubByMember(Context.User);
                if (club == null)
                {
                    await ReplyErrorLocalized("club_null").ConfigureAwait(false);
                    return;
                }

                if (club.Owner.UserId != Context.User.Id)
                {
                    await ReplyErrorLocalized("club_not_owner").ConfigureAwait(false);
                    return;
                }

                if (club.roleId == 0)
                {
                    await ReplyErrorLocalized("club_role_not_exists").ConfigureAwait(false);
                    return;
                }

                if (club.XpImageUrl != "")
                {
                    await ReplyErrorLocalized("club_xp_image_exists").ConfigureAwait(false);
                    return;
                }

                if (club.Currency < 1000000)
                {
                    await ReplyErrorLocalized("club_not_enough").ConfigureAwait(false);
                    return;
                }

                if (_service.XpImageCreate(Context.User, url))
                    await ReplyConfirmLocalized("club_xp_card_created").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubXpImageUpdate([Remainder]string url = null)
            {
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) && url != null)
                {
                    await ReplyErrorLocalized("club_image_error").ConfigureAwait(false);
                    return;
                }

                var club = _service.GetClubByMember(Context.User);
                if (club == null)
                {
                    await ReplyErrorLocalized("club_null").ConfigureAwait(false);
                    return;
                }

                if (club.Owner.UserId != Context.User.Id)
                {
                    await ReplyErrorLocalized("club_not_owner").ConfigureAwait(false);
                    return;
                }

                if (club.roleId == 0)
                {
                    await ReplyErrorLocalized("club_role_not_exists").ConfigureAwait(false);
                    return;
                }

                if (club.XpImageUrl == "")
                {
                    await ReplyErrorLocalized("club_xp_image_not_exists").ConfigureAwait(false);
                    return;
                }

                if (club.Currency < 10000)
                {
                    await ReplyErrorLocalized("club_not_enough").ConfigureAwait(false);
                    return;
                }

                if (_service.XpImageUpdate(Context.User, url))
                    await ReplyConfirmLocalized("club_xp_card_updated").ConfigureAwait(false);
            }
        }
    }
}
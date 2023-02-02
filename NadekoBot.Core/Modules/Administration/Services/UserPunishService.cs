using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Common.TypeReaders.Models;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Extensions;

namespace NadekoBot.Modules.Administration.Services
{
    public class UserPunishService : INService
    {
        private readonly MuteService _mute;
        private readonly DbService _db;

        public UserPunishService(MuteService mute, DbService db)
        {
            _mute = mute;
            _db = db;
        }

        public async Task<WarningPunishment> Warn(IGuild guild, IUser user, IUser mod, string reason)
        {
            var modName = mod.ToString();

            if (string.IsNullOrWhiteSpace(reason))
                reason = "-";

            var guildId = guild.Id;

            var warn = new Warning()
            {
                UserId = user.Id,
                GuildId = guildId,
                Forgiven = false,
                Reason = reason,
                Moderator = modName,
            };

            var log = new ModLog()
            {
                UserId = user.Id,
                GuildId = guildId,
                Type = "Warn",
                Reason = reason,
                Moderator = mod.Id,
            };

            int warnings = 1;
            List<WarningPunishment> ps;
            using (var uow = _db.UnitOfWork)
            {
                ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments))
                    .WarnPunishments;

                if (uow.Warnings.ForId(guildId, user.Id).FirstOrDefault() != null)
                {
                    DateTime lastDate = (DateTime)uow.Warnings.ForId(guildId, user.Id).First().DateAdded;

                    if (lastDate != null && DateTime.Compare(lastDate, DateTime.UtcNow.AddDays(-180)) < 0)
                    {
                        await uow.Warnings.ForgiveAll(guildId, user.Id, "Expiration Date");
                    }
                }

                warnings += uow.Warnings
                    .ForId(guildId, user.Id)
                    .Where(w => !w.Forgiven && w.UserId == user.Id)
                    .Count();

                uow.Warnings.Add(warn);
                uow.ModLog.Add(log);

                var du = uow.DiscordUsers.GetOrCreate(user);
                var w = uow.Waifus.ByWaifuUserId(user.Id);
                var thisUser = uow.DiscordUsers.GetOrCreate(user);

                if (w == null)
                {
                    uow.Waifus.Add(new WaifuInfo()
                    {
                        Waifu = thisUser,
                        Price = 1,
                        Claimer = null,
                        Immune = false,
                        Reputation = -100
                    });
                }
                else
                    w.Reputation -= 100;

                uow.RepLog.Add(new RepLog()
                {
                    UserId = thisUser.UserId,
                    FromId = 0
                });

                uow.Complete();
            }

            var p = ps.FirstOrDefault(x => x.Count == warnings);

            if (p != null)
            {
                var guser = await guild.GetUserAsync(user.Id).ConfigureAwait(false);
                if (guser == null)
                    return null;
                switch (p.Punishment)
                {
                    case PunishmentAction.Mute:
                        if (p.Time == 0)
                            await _mute.MuteUser(guser, mod).ConfigureAwait(false);
                        else
                            await _mute.TimedMute(guser, mod, TimeSpan.FromMinutes(p.Time)).ConfigureAwait(false);
                        break;
                    case PunishmentAction.Kick:
                        await guser.KickAsync("Warned too many times.").ConfigureAwait(false);
                        break;
                    case PunishmentAction.Ban:
                        if (p.Time == 0)
                            await guild.AddBanAsync(guser, reason: "Warned too many times.").ConfigureAwait(false);
                        else
                            await _mute.TimedBan(guser, TimeSpan.FromMinutes(p.Time), "Warned too many times.").ConfigureAwait(false);
                        break;
                    case PunishmentAction.Softban:
                        await guild.AddBanAsync(guser, 7, reason: "Warned too many times").ConfigureAwait(false);
                        try
                        {
                            await guild.RemoveBanAsync(guser).ConfigureAwait(false);
                        }
                        catch
                        {
                            await guild.RemoveBanAsync(guser).ConfigureAwait(false);
                        }
                        break;
                    case PunishmentAction.RemoveRoles:
                        await guser.RemoveRolesAsync(guser.GetRoles().Where(x => x.Id != guild.EveryoneRole.Id)).ConfigureAwait(false);
                        break;
                    default:
                        break;
                }
                return p;
            }

            return null;
        }

        public IGrouping<ulong, Warning>[] WarnlogAll(ulong gid)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.Warnings.GetForGuild(gid).GroupBy(x => x.UserId).ToArray();
            }
        }

        public Warning[] UserWarnings(ulong gid, ulong userId)
        {
            using (var uow = _db.UnitOfWork)
            {
                if (uow.Warnings.ForId(gid, userId).FirstOrDefault() != null)
                {
                    DateTime lastDate = (DateTime)uow.Warnings.ForId(gid, userId).First().DateAdded;

                    if (DateTime.Compare(lastDate, DateTime.UtcNow.AddDays(-180)) < 0)
                    {
                        uow.Warnings.ForgiveAll(gid, userId, "Expiration Date");
                    }

                    uow.Complete();
                }

                return uow.Warnings.ForId(gid, userId);
            }
        }

        public ModLog[] UserModLogs(ulong gid, ulong userId)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.ModLog.ForId(gid, userId);
            }
        }

        public ModLog[] ModeratorStats(ulong gid, ulong moderator)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.ModLog.ByModerator(gid, moderator);
            }
        }

        public IGrouping<ulong, ModLog>[] AllStats(ulong gid)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.ModLog.ByGuild(gid).GroupBy(x => x.Moderator).ToArray();
            }
        }

        public string GetUserById(ulong id)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.DiscordUsers.GetUsernameByUserId(id);
            }
        }

        public async Task<bool> WarnClearAsync(ulong guildId, IGuildUser user, int index, string moderator)
        {
            bool toReturn = true;
            using (var uow = _db.UnitOfWork)
            {
                if (index == 0)
                {
                    await uow.Warnings.ForgiveAll(guildId, user.Id, moderator);
                }
                else
                {
                    toReturn = uow.Warnings.Forgive(guildId, user.Id, moderator, index - 1);

                    var du = uow.DiscordUsers.GetOrCreate(user);
                    var w = uow.Waifus.ByWaifuUserId(user.Id);
                    var thisUser = uow.DiscordUsers.GetOrCreate(user);

                    if (w == null)
                    {
                        uow.Waifus.Add(new WaifuInfo()
                        {
                            Waifu = thisUser,
                            Price = 1,
                            Claimer = null,
                            Immune = false,
                            Reputation = 0
                        });
                    }
                    //else
                        //w.Reputation += 100;

                    uow.RepLog.Add(new RepLog()
                    {
                        UserId = thisUser.UserId,
                        FromId = 1
                    });

                }
                uow.Complete();
            }
            return toReturn;
        }

        public bool WarnPunish(ulong guildId, int number, PunishmentAction punish, StoopidTime time)
        {
            if ((punish != PunishmentAction.Ban && punish != PunishmentAction.Mute) && time != null)
                return false;
            if (number <= 0 || (time != null && time.Time > TimeSpan.FromDays(49)))
                return false;

            using (var uow = _db.UnitOfWork)
            {
                var ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments)).WarnPunishments;
                var toDelete = ps.Where(x => x.Count == number);

                uow._context.RemoveRange(toDelete);

                ps.Add(new WarningPunishment()
                {
                    Count = number,
                    Punishment = punish,
                    Time = (int?)(time?.Time.TotalMinutes) ?? 0,
                });
                uow.Complete();
            }
            return true;
        }

        public bool WarnPunish(ulong guildId, int number)
        {
            if (number <= 0)
                return false;

            using (var uow = _db.UnitOfWork)
            {
                var ps = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.WarnPunishments)).WarnPunishments;
                var p = ps.FirstOrDefault(x => x.Count == number);

                if (p != null)
                {
                    uow._context.Remove(p);
                    uow.Complete();
                }
            }
            return true;
        }

        public WarningPunishment[] WarnPunishList(ulong guildId)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.GuildConfigs.ForId(guildId, gc => gc.Include(x => x.WarnPunishments))
                    .WarnPunishments
                    .OrderBy(x => x.Count)
                    .ToArray();
            }
        }

        public (IEnumerable<(string Original, ulong? Id, string Reason)> Bans, int Missing) MassKill(SocketGuild guild, string people)
        {
            var gusers = guild.Users;
            //get user objects and reasons
            var bans = people.Split("\n")
                .Select(x =>
                {
                    var split = x.Trim().Split(" ");

                    var reason = string.Join(" ", split.Skip(1));

                    if (ulong.TryParse(split[0], out var id))
                        return (Original: split[0], Id: id, Reason: reason);

                    return (Original: split[0],
                        Id: gusers
                            .FirstOrDefault(u => u.ToString().ToLowerInvariant() == x)
                            ?.Id,
                        Reason: reason);
                })
                .ToArray();

            //if user is null, means that person couldn't be found
            var missing = bans
                .Where(x => !x.Id.HasValue)
                .Count();

            //get only data for found users
            var found = bans
                .Where(x => x.Id.HasValue)
                .Select(x => x.Id.Value)
                .ToArray();

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.Blacklist));
                //blacklist the users
                bc.Blacklist.AddRange(found.Select(x =>
                    new BlacklistItem
                    {
                        ItemId = x,
                        Type = BlacklistType.User,
                    }));
                //clear their currencies
                uow.DiscordUsers.RemoveFromMany(found.Select(x => x).ToList());
                uow.Complete();
            }

            return (bans, missing);
        }

        public string GetTime(double t)
        {
            string result = "";

            if (t < 1)
                result = (t * 60) + " минут";
            else
                if (t == 1)
                result = t + " час";
            else
                if (t < 5)
                result = t + " часа";
            else
                if (t < 24)
                result = t + " часов";
            else
                if (t == 24)
                result = (t / 24) + " день";
            else
                if (t < 120)
                result = (t / 24) + " дня";
            else
                result = (t / 24) + " дней";

            return result;
        }
    }
}

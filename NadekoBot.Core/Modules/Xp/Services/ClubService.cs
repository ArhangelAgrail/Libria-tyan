using NadekoBot.Core.Services;
using System;
using NadekoBot.Core.Services.Database.Models;
using Discord;
using NadekoBot.Modules.Xp.Common;
using System.Linq;
using NadekoBot.Extensions;
using System.Net.Http;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Xp.Services
{
    public class ClubService : INService
    {
        private readonly DbService _db;
        private readonly IHttpClientFactory _httpFactory;

        public ClubService(DbService db, IHttpClientFactory _httpFactory)
        {
            _db = db;
            this._httpFactory = _httpFactory;
        }

        public bool CreateClub(IUser user, string clubName, out ClubInfo club)
        {
            //must be lvl 5 and must not be in a club already

            club = null;
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);
                uow._context.SaveChanges();
                var xp = new LevelStats(du.TotalXp);

                if (xp.Level >= 5 && du.Club == null)
                {
                    du.IsClubAdmin = true;
                    du.Club = new ClubInfo()
                    {
                        Name = clubName,
                        Discrim = uow.Clubs.GetNextDiscrim(clubName),
                        Owner = du,
                    };
                    du.ClubXp = du.TotalXp;
                    uow.Clubs.Add(du.Club);
                    uow._context.SaveChanges();
                    du.ClubInvetsAmount = 0;
                }
                else
                    return false;

                uow._context.Set<ClubApplicants>()
                    .RemoveRange(uow._context.Set<ClubApplicants>().Where(x => x.UserId == du.Id));
                club = du.Club;
                uow.Complete();
            }

            return true;
        }

        public ClubInfo TransferClub(IUser from, IUser newOwner)
        {
            ClubInfo club;
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwner(from.Id);
                var newOwnerUser = uow.DiscordUsers.GetOrCreate(newOwner);

                if (club == null ||
                    club.Owner.UserId != from.Id ||
                    !club.Users.Contains(newOwnerUser))
                    return null;

                club.Owner.IsClubAdmin = true; // old owner will stay as admin
                newOwnerUser.IsClubAdmin = true;
                club.Owner = newOwnerUser;

                var guildUser = from as IGuildUser;
                var roles = uow.Achievements.ByGroup("ClubLead");
                var roleIds = roles.Select(x => x.RoleId).ToArray();
                var sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                foreach (var roleId in sameRoles)
                {
                    var sameRole = guildUser.Guild.GetRole(roleId);
                    if (sameRole != null)
                    {
                        try
                        {
                            guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                            Task.Delay(50).ConfigureAwait(false);
                        }
                        catch
                        { }
                    }
                }

                guildUser = newOwner as IGuildUser;
                sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                IRole role = null;
                foreach (var cond in roles)
                {
                    if (club.Users.Count >= cond.Condition)
                        role = guildUser.Guild.GetRole(cond.RoleId);
                }

                foreach (var roleId in sameRoles)
                {
                    var sameRole = guildUser.Guild.GetRole(roleId);
                    if (role != sameRole)
                        if (sameRole != null)
                        {
                            try
                            {
                                guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                if (role != null)
                {
                    try
                    {
                        guildUser.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch
                    { }
                }

                uow.Complete();
            }
            return club;
        }

        public bool ToggleAdmin(IUser owner, IUser toAdmin)
        {
            bool newState;
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(owner.Id);
                var adminUser = uow.DiscordUsers.GetOrCreate(toAdmin);

                if (club == null || club.Owner.UserId != owner.Id ||
                    !club.Users.Contains(adminUser))
                    throw new InvalidOperationException();

                if (club.OwnerId == adminUser.Id)
                    return true;

                newState = adminUser.IsClubAdmin = !adminUser.IsClubAdmin;
                uow.Complete();
            }
            return newState;
        }

        public ClubInfo GetClubByMember(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.Clubs.GetByMember(user.Id);
            }
        }

        public async Task<bool> SetClubIcon(ulong ownerUserId, Uri url)
        {
            if (url != null)
            {
                using (var http = _httpFactory.CreateClient())
                using (var temp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    if (!temp.IsImage() || temp.GetImageSize() > 11)
                        return false;
                }
            }

            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(ownerUserId, set => set);

                if (club == null)
                    return false;

                club.ImageUrl = url.ToString();
                uow.Complete();
            }

            return true;
        }

        public bool GetClubByName(string clubName, out ClubInfo club)
        {
            club = null;
            //var arr = clubName.Split('#');
            //if (arr.Length < 2 || !int.TryParse(arr[arr.Length - 1], out var discrim))
                //return false;

            //incase club has # in it
            //var name = string.Concat(arr.Except(new[] { arr[arr.Length - 1] }));

            if (string.IsNullOrWhiteSpace(clubName))
                return false;

            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByName(clubName/*, discrim*/);
                if (club == null)
                    return false;
                else
                    return true;
            }
        }

        public bool ApplyToClub(IUser user, ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);
                uow._context.SaveChanges();

                if (du.Club != null
                    || new LevelStats(du.TotalXp).Level < club.MinimumLevelReq
                    || club.Bans.Any(x => x.UserId == du.Id)
                    || club.Applicants.Any(x => x.UserId == du.Id))
                {
                    //user banned or a member of a club, or already applied,
                    // or doesn't min minumum level requirement, can't apply
                    return false;
                }

                var app = new ClubApplicants
                {
                    ClubId = club.Id,
                    UserId = du.Id,
                };

                uow._context.Set<ClubApplicants>().Add(app);

                uow.Complete();
            }
            return true;
        }

        public bool AcceptApplication(ulong clubOwnerUserId, string userName, IGuild guild, out DiscordUser discordUser)
        {
            discordUser = null;
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwnerOrAdmin(clubOwnerUserId);
                if (club == null)
                    return false;

                var applicant = club.Applicants.FirstOrDefault(x => x.User.ToString().ToUpperInvariant() == userName.ToUpperInvariant());

                applicant.User.Club = club;
                applicant.User.IsClubAdmin = false;
                applicant.User.ClubXp = applicant.User.TotalXp;
                applicant.User.ClubInvetsAmount = Convert.ToInt32(GetAmountByUserOld(applicant.User.UserId, club.Name)) * -1;
                club.Applicants.Remove(applicant);

                //remove that user's all other applications
                uow._context.Set<ClubApplicants>()
                    .RemoveRange(uow._context.Set<ClubApplicants>().Where(x => x.UserId == applicant.User.Id));
                discordUser = applicant.User;

                var guildUser = guild.GetUserAsync(club.Owner.UserId).Result;
                var roles = uow.Achievements.ByGroup("ClubLead");
                var roleIds = roles.Select(x => x.RoleId).ToArray();
                var sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                IRole role = null;
                foreach (var cond in roles)
                {
                    if (club.Users.Count+1 >= cond.Condition)
                        role = guildUser.Guild.GetRole(cond.RoleId);
                }

                foreach (var roleId in sameRoles)
                {
                    var sameRole = guildUser.Guild.GetRole(roleId);
                    if (role != sameRole)
                        if (sameRole != null)
                        {
                            try
                            {
                                guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                if (role != null)
                {
                    try
                    {
                        guildUser.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch
                    { }
                }

                uow.Complete();
            }
            return true;
        }

        public ClubInfo GetClubWithBansAndApplications(ulong ownerUserId)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.Clubs.GetByOwnerOrAdmin(ownerUserId);
            }
        }

        public bool LeaveClub(IUser user, IGuild guild)
        {
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);
                if (du.Club == null || du.Club.OwnerId == du.Id)
                    return false;

                var club = GetClubByMember(user);

                du.Club = null;
                du.IsClubAdmin = false;

                var guildUser = guild.GetUserAsync(club.Owner.UserId).Result;
                var roles = uow.Achievements.ByGroup("ClubLead");
                var roleIds = roles.Select(x => x.RoleId).ToArray();
                var sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                IRole role = null;
                foreach (var cond in roles)
                {
                    if (club.Users.Count >= cond.Condition)
                        role = guildUser.Guild.GetRole(cond.RoleId);
                }

                foreach (var roleId in sameRoles)
                {
                    var sameRole = guildUser.Guild.GetRole(roleId);
                    if (role != sameRole)
                        if (sameRole != null)
                        {
                            try
                            {
                                guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                if (role != null)
                {
                    try
                    {
                        guildUser.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch
                    { }
                }

                uow.Complete();
            }
            return true;
        }

        public bool ChangeClubLevelReq(ulong userId, int level)
        {
            if (level < 5)
                return false;

            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(userId);
                if (club == null)
                    return false;
                
                club.MinimumLevelReq = level;
                uow.Complete();
            }

            return true;
        }

        public bool ChangeClubDescription(ulong userId, string desc)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(userId);
                if (club == null)
                    return false;

                club.Description = desc?.TrimTo(150, true);
                uow.Complete();
            }

            return true;
        }

        public bool Disband(ulong userId, IGuild guild, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwner(userId);
                if (club == null)
                    return false;

                var guildUser = guild.GetUserAsync(club.Owner.UserId).Result;
                var roles = uow.Achievements.ByGroup("ClubLead");
                var roleIds = roles.Select(x => x.RoleId).ToArray();
                var sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                foreach (var roleId in sameRoles)
                {
                    var sameRole = guildUser.Guild.GetRole(roleId);
                        if (sameRole != null)
                        {
                            try
                            {
                                guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                uow.Clubs.Remove(club);
                uow.Complete();
            }
            return true;
        }

        public bool Disband(ClubInfo club, IGuild guild)
        {
            using (var uow = _db.UnitOfWork)
            {
                if (club == null)
                    return false;

                var guildUser = guild.GetUserAsync(club.Owner.UserId).Result;
                var roles = uow.Achievements.ByGroup("ClubLead");
                var roleIds = roles.Select(x => x.RoleId).ToArray();
                var sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                foreach (var roleId in sameRoles)
                {
                    var sameRole = guildUser.Guild.GetRole(roleId);
                        if (sameRole != null)
                        {
                            try
                            {
                                guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                uow.Clubs.Remove(club);
                uow.Complete();
            }
            return true;
        }

        public async Task<bool> RoleCreate(IUser user, IRole role)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(user.Id);

                var users = club.Users;
                var du = user as IGuildUser;

                club.roleId = role.Id;
                club.Currency -= 500000;
                uow.Complete();

                foreach (var usrs in users)
                {
                    try
                    {
                        var usr = await du.Guild.GetUserAsync(usrs.UserId);
                        await usr.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch { };
                }
            }
            return true;
        }

        public bool TextCreate(IUser user, ITextChannel text)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(user.Id);

                var users = club.Users;
                var du = user as IGuildUser;

                club.textId = text.Id;
                club.Currency -= 5000000;
                uow.Complete();
            }
            return true;
        }

        public bool PlaceAdd(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(user.Id);

                club.Members += 1;
                club.Currency -= 50000;
                uow.Complete();
            }
            return true;
        }

        public bool XpImageCreate(IUser user, string url)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(user.Id);

                club.XpImageUrl = url;
                club.Currency -= 1000000;
                uow.Complete();
            }
            return true;
        }

        public bool XpImageUpdate(IUser user, string url)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByOwner(user.Id);

                club.XpImageUrl = url;
                club.Currency -= 10000;
                uow.Complete();
            }
            return true;
        }

        public bool Ban(ulong bannerId, DiscordUser usr, IGuild guild, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwnerOrAdmin(bannerId);
                if (club == null)
                    return false;

                club.Bans.Add(new ClubBans
                {
                    Club = club,
                    User = usr,
                });
                club.Users.Remove(usr);

                var app = club.Applicants.FirstOrDefault(x => x.UserId == usr.Id);
                if (app != null)
                    club.Applicants.Remove(app);

                var guildUser = guild.GetUserAsync(club.Owner.UserId).Result;
                var roles = uow.Achievements.ByGroup("ClubLead");
                var roleIds = roles.Select(x => x.RoleId).ToArray();
                var sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                IRole role = null;
                foreach (var cond in roles)
                {
                    if (club.Users.Count >= cond.Condition)
                        role = guildUser.Guild.GetRole(cond.RoleId);
                }

                foreach (var roleId in sameRoles)
                {
                    var sameRole = guildUser.Guild.GetRole(roleId);
                    if (role != sameRole)
                        if (sameRole != null)
                        {
                            try
                            {
                                guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                if (role != null)
                {
                    try
                    {
                        guildUser.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch
                    { }
                }

                uow.Complete();
            }

            return true;
        }

        public bool Decline(ulong declinerId, DiscordUser usr, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwnerOrAdmin(declinerId);
                if (club == null)
                    return false;

                var app = club.Applicants.FirstOrDefault(x => x.UserId == usr.Id);
                if (app != null)
                    club.Applicants.Remove(app);
                else return false;

                uow.Complete();
            }

            return true;
        }

        public bool UnBan(ulong ownerUserId, string userName, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwnerOrAdmin(ownerUserId);
                if (club == null)
                    return false;

                var ban = club.Bans.FirstOrDefault(x => x.User.ToString().ToUpperInvariant() == userName.ToUpperInvariant());
                if (ban == null)
                    return false;

                club.Bans.Remove(ban);
                uow.Complete();
            }

            return true;
        }

        public bool Kick(ulong kickerId, DiscordUser usr, IGuild guild, out ClubInfo club)
        {
            using (var uow = _db.UnitOfWork)
            {
                club = uow.Clubs.GetByOwnerOrAdmin(kickerId);
                if (club == null)
                    return false;

                club.Users.Remove(usr);
                var app = club.Applicants.FirstOrDefault(x => x.UserId == usr.Id);
                if (app != null)
                    club.Applicants.Remove(app);

                var guildUser = guild.GetUserAsync(club.Owner.UserId).Result;
                var roles = uow.Achievements.ByGroup("ClubLead");
                var roleIds = roles.Select(x => x.RoleId).ToArray();
                var sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                IRole role = null;
                foreach (var cond in roles)
                {
                    if (club.Users.Count-1 >= cond.Condition)
                        role = guildUser.Guild.GetRole(cond.RoleId);
                }

                foreach (var roleId in sameRoles)
                {
                    var sameRole = guildUser.Guild.GetRole(roleId);
                    if (role != sameRole)
                        if (sameRole != null)
                        {
                            try
                            {
                                guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                if (role != null)
                {
                    try
                    {
                        guildUser.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch
                    { }
                }

                uow.Complete();
            }

            return true;
        }

        public ClubInfo[] GetClubLeaderboardPage(int page)
        {
            if (page < 0)
                throw new ArgumentOutOfRangeException(nameof(page));

            using (var uow = _db.UnitOfWork)
            {
                return uow.Clubs.GetClubLeaderboardPage(page);
            }
        }

        public int GetAmountByUser(ulong userId)
        {
            using (var uow = _db.UnitOfWork)
            {
                var user = uow.DiscordUsers.Get(userId);
                return user.ClubInvetsAmount;
            }
        }

        public string GetAmountByUserOld(ulong userId, string clubName)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.CurrencyTransactions.GetInvestedAmount(userId, clubName);
            }
        }

        public int SetAmountByUser(ulong userId, int sum)
        {
            using (var uow = _db.UnitOfWork)
            {
                var user = uow.DiscordUsers.Get(userId);
                user.ClubInvetsAmount = sum;
                uow.Complete();
                return user.ClubInvetsAmount;
            }
        }

        public bool StorageAward(int amount, string clubName)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByName(clubName);

                club.Currency += amount;
                uow.Complete();

                return true;
            }
        }

        public string GetStorageProgress(int amount, int maxAmount)
        {
            double percent = (double)amount / (double)maxAmount;
            string result = "";

            if (percent < 0.1)
                result = "▬▬▬▬▬▬▬▬▬▬";
            else
                if (percent < 0.2)
                result = "[▬](https://anilibria.tv)▬▬▬▬▬▬▬▬▬";
            else
                if (percent < 0.3)
                result = "[▬▬](https://anilibria.tv)▬▬▬▬▬▬▬▬";
            else
                if (percent < 0.4)
                result = "[▬▬▬](https://anilibria.tv)▬▬▬▬▬▬▬";
            else
                if (percent < 0.5)
                result = "[▬▬▬▬](https://anilibria.tv)▬▬▬▬▬▬";
            else
                if (percent < 0.6)
                result = "[▬▬▬▬▬](https://anilibria.tv)▬▬▬▬▬";
            else
                if (percent < 0.7)
                result = "[▬▬▬▬▬▬](https://anilibria.tv)▬▬▬▬";
            else
                if (percent < 0.8)
                result = "[▬▬▬▬▬▬▬](https://anilibria.tv)▬▬▬";
            else
                if (percent < 0.9)
                result = "[▬▬▬▬▬▬▬▬](https://anilibria.tv)▬▬";
            else
                if (percent < 1)
                result = "[▬▬▬▬▬▬▬▬▬](https://anilibria.tv)▬";
            else
                result = "[▬▬▬▬▬▬▬▬▬▬](https://anilibria.tv)";

            return result;
        }
    }
}

using Discord;
using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Modules.Gambling.Common.Waifu;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Core.Services.Database.Repositories;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Gambling.Services
{
    public class WaifuService : INService
    {
        public class FullWaifuInfo
        {
            public WaifuInfo Waifu { get; set; }
            public IEnumerable<string> Claims { get; set; }
            public int Divorces { get; set; }
        }

        private readonly DbService _db;
        private readonly ICurrencyService _cs;
        private readonly IBotConfigProvider _bc;
        private readonly IDataCache _cache;
        private readonly Logger _log;

        public WaifuService(DbService db, ICurrencyService cs, IBotConfigProvider bc, IDataCache cache)
        {
            _db = db;
            _cs = cs;
            _bc = bc;
            _cache = cache;
            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task<bool> SetImmune(IUser target)
        {
            var success = false;
            using (var uow = _db.UnitOfWork)
            {
                var w = uow.Waifus.ByWaifuUserId(target.Id);

                if (w == null)
                {
                    if (!await _cs.RemoveAsync(target.Id, "Immune set", 10000, gamble: true))
                        return false;

                    var thisUser = uow.DiscordUsers.GetOrCreate(target);
                    uow.Waifus.Add(new WaifuInfo()
                    {
                        Waifu = thisUser,
                        Price = 1,
                        Claimer = null,
                        Immune = true,
                        Reputation = 0
                    });
                    success = true;
                }
                else
                    if (w.Immune != true)
                    {
                    if (!await _cs.RemoveAsync(target.Id, "Immune set", 10000, gamble: true))
                        return false;

                        w.Immune = true;
                        success = true;
                    }
                else
                {
                    if (!await _cs.RemoveAsync(target.Id, "Immune set", 10000, gamble: true))
                        return false;
                    w.Immune = false;
                    success = true;
                }

               await uow.CompleteAsync();
            }

            return (success);
        }

        public async Task<bool> SetInfo(IUser target, string info)
        {
            var success = false;
            using (var uow = _db.UnitOfWork)
            {
                var w = uow.Waifus.ByWaifuUserId(target.Id);

                if (w == null)
                {
                    var thisUser = uow.DiscordUsers.GetOrCreate(target);
                    uow.Waifus.Add(new WaifuInfo()
                    {
                        Waifu = thisUser,
                        Price = 1,
                        Claimer = null,
                        Immune = false,
                        Reputation = 0,
                        Info = info
                    });
                }
                else
                    {
                        w.Info = info;
                        success = true;
                    }

                await uow.CompleteAsync();
            }

            return (success);
        }

        public async Task<bool> WaifuTransfer(IUser owner, ulong waifuId, IUser newOwner)
        {
            if (owner.Id == newOwner.Id || waifuId == newOwner.Id)
                return false;
            using (var uow = _db.UnitOfWork)
            {
                var waifu = uow.Waifus.ByWaifuUserId(waifuId);
                var ownerUser = uow.DiscordUsers.GetOrCreate(owner);

                // owner has to be the owner of the waifu
                if (waifu == null || waifu.ClaimerId != ownerUser.Id)
                    return false;

                if (!await _cs.RemoveAsync(owner.Id, "Waifu Transfer",
                    waifu.Price / 10, gamble: true))
                {
                    return false;
                }

                //new claimerId is the id of the new owner
                var newOwnerUser = uow.DiscordUsers.GetOrCreate(newOwner);
                waifu.ClaimerId = newOwnerUser.Id;

                await uow.CompleteAsync();
            }

            return true;
        }

        public int GetResetPrice(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var waifu = uow.Waifus.ByWaifuUserId(user.Id);

                if (waifu == null)
                    return _bc.BotConfig.MinWaifuPrice;

                var divorces = uow._context.WaifuUpdates.Count(x => x.Old != null &&
                        x.Old.UserId == user.Id &&
                        x.UpdateType == WaifuUpdateType.Claimed &&
                        x.New == null);
                var affs = uow._context.WaifuUpdates
                        .Where(w => w.User.UserId == user.Id && w.UpdateType == WaifuUpdateType.AffinityChanged && w.New != null)
                        .GroupBy(x => x.New)
                        .Count();

                return (int)Math.Ceiling(waifu.Price * 1.25f) + ((divorces + affs + 2) * _bc.BotConfig.DivorcePriceMultiplier);
            }
        }

        public async Task<bool> TryReset(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var price = GetResetPrice(user);
                if (!await _cs.RemoveAsync(user.Id, "Waifu Reset", price, gamble: true))
                    return false;

                var affs = uow._context.WaifuUpdates
                    .Where(w => w.User.UserId == user.Id
                        && w.UpdateType == WaifuUpdateType.AffinityChanged
                        && w.New != null);

                var divorces = uow._context.WaifuUpdates.Where(x => x.Old != null &&
                        x.Old.UserId == user.Id &&
                        x.UpdateType == WaifuUpdateType.Claimed &&
                        x.New == null);

                //reset changes of heart to 0
                uow._context.WaifuUpdates.RemoveRange(affs);
                //reset divorces to 0
                uow._context.WaifuUpdates.RemoveRange(divorces);
                var waifu = uow.Waifus.ByWaifuUserId(user.Id);
                //reset price, remove items
                //remove owner, remove affinity
                waifu.Price = 50;
                waifu.Items.Clear();
                waifu.ClaimerId = null;
                waifu.AffinityId = null;

                //wives stay though

                uow.Complete();
            }
            return true;
        }

        public async Task<(WaifuInfo, bool, WaifuClaimResult)> ClaimWaifuAsync(IUser user, IUser target, int amount, int count)
        {
            WaifuClaimResult result;
            WaifuInfo w, u;
            bool isAffinity;
            using (var uow = _db.UnitOfWork)
            {
                w = uow.Waifus.ByWaifuUserId(target.Id);
                u = uow.Waifus.ByWaifuUserId(user.Id);

                isAffinity = (w?.Affinity?.UserId == user.Id);
                if (w == null)
                {
                    var waifu = uow.DiscordUsers.GetOrCreate(target);
                    uow.Waifus.Add(w = new WaifuInfo()
                    {
                        Waifu = waifu,
                        Affinity = null,
                        Claimer = null,
                        Price = 1,
                        Immune = false,
                        Reputation = 0
                    });
                }
                if (w.Immune == false && u.Immune == false)
                {
                    if (count < 7)
                    {
                        if (w == null)
                        {
                            var claimer = uow.DiscordUsers.GetOrCreate(user);
                            var waifu = uow.DiscordUsers.GetOrCreate(target);
                            if (!await _cs.RemoveAsync(user.Id, "Claimed Waifu", amount, gamble: true))
                            {
                                result = WaifuClaimResult.NotEnoughFunds;
                            }
                            else
                            {
                                uow.Waifus.Add(w = new WaifuInfo()
                                {
                                    Waifu = waifu,
                                    Claimer = claimer,
                                    Affinity = null,
                                    Price = amount
                                });
                                uow._context.WaifuUpdates.Add(new WaifuUpdate()
                                {
                                    User = waifu,
                                    Old = null,
                                    New = claimer,
                                    UpdateType = WaifuUpdateType.Claimed
                                });
                                result = WaifuClaimResult.Success;
                            }
                        }
                        else if (isAffinity && amount > w.Price * 0.88f)
                        {
                            if (!await _cs.RemoveAsync(user.Id, "Claimed Waifu", amount, gamble: true))
                            {
                                result = WaifuClaimResult.NotEnoughFunds;
                            }
                            else
                            {
                                var oldClaimer = w.Claimer;
                                w.Claimer = uow.DiscordUsers.GetOrCreate(user);
                                w.Price = amount + (amount / 4);
                                result = WaifuClaimResult.Success;

                                uow._context.WaifuUpdates.Add(new WaifuUpdate()
                                {
                                    User = w.Waifu,
                                    Old = oldClaimer,
                                    New = w.Claimer,
                                    UpdateType = WaifuUpdateType.Claimed
                                });
                            }
                        }
                        else if (amount >= w.Price * 1.1f) // if no affinity
                        {
                            if (!await _cs.RemoveAsync(user.Id, "Claimed Waifu", amount, gamble: true))
                            {
                                result = WaifuClaimResult.NotEnoughFunds;
                            }
                            else
                            {
                                var oldClaimer = w.Claimer;
                                w.Claimer = uow.DiscordUsers.GetOrCreate(user);
                                w.Price = amount;
                                result = WaifuClaimResult.Success;

                                uow._context.WaifuUpdates.Add(new WaifuUpdate()
                                {
                                    User = w.Waifu,
                                    Old = oldClaimer,
                                    New = w.Claimer,
                                    UpdateType = WaifuUpdateType.Claimed
                                });
                            }
                        }
                        else
                            result = WaifuClaimResult.InsufficientAmount;
                    }
                    else
                        result = WaifuClaimResult.MaxCount;
                }
                else
                    result = WaifuClaimResult.Immune;

                await uow.CompleteAsync();
            }

            return (w, isAffinity, result);
        }

        public async Task<(DiscordUser, bool, TimeSpan?)> ChangeAffinityAsync(IUser user, IGuildUser target)
        {
            DiscordUser oldAff = null;
            var success = false;
            TimeSpan? remaining = null;
            using (var uow = _db.UnitOfWork)
            {
                var w = uow.Waifus.ByWaifuUserId(user.Id);
                var newAff = target == null ? null : uow.DiscordUsers.GetOrCreate(target);
                var now = DateTime.UtcNow;
                if (w?.Affinity?.UserId == target?.Id)
                {

                }
                else if (!_cache.TryAddAffinityCooldown(user.Id, out remaining))
                {
                }
                else if (w == null)
                {
                    var thisUser = uow.DiscordUsers.GetOrCreate(user);
                    uow.Waifus.Add(new WaifuInfo()
                    {
                        Affinity = newAff,
                        Waifu = thisUser,
                        Price = 1,
                        Claimer = null
                    });
                    success = true;

                    uow._context.WaifuUpdates.Add(new WaifuUpdate()
                    {
                        User = thisUser,
                        Old = null,
                        New = newAff,
                        UpdateType = WaifuUpdateType.AffinityChanged
                    });
                }
                else
                {
                    if (w.Affinity != null)
                        oldAff = w.Affinity;
                    w.Affinity = newAff;
                    success = true;

                    uow._context.WaifuUpdates.Add(new WaifuUpdate()
                    {
                        User = w.Waifu,
                        Old = oldAff,
                        New = newAff,
                        UpdateType = WaifuUpdateType.AffinityChanged
                    });
                }

                await uow.CompleteAsync();
            }

            return (oldAff, success, remaining);
        }

        public IEnumerable<WaifuLbResult> GetTopWaifusAtPage(int page)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.Waifus.GetTop(9, page * 9);
            }
        }

        public IEnumerable<RepLbResult> GetTopRepAtPage(int page)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.Waifus.GetRepLb(10, page * 10);
            }
        }

        public IEnumerable<RepLogResult> GetRepLogByUser(ulong userId, int page)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.RepLog.GetRepLog(userId, 9, page * 9);
            }
        }

        public async Task<(WaifuInfo, DivorceResult, long, TimeSpan?)> DivorceWaifuAsync(IUser user, ulong targetId)
        {
            DivorceResult result;
            TimeSpan? remaining = null;
            long amount = 0;
            WaifuInfo w = null;
            using (var uow = _db.UnitOfWork)
            {
                w = uow.Waifus.ByWaifuUserId(targetId);
                var now = DateTime.UtcNow;
                if (w?.Claimer == null || w.Claimer.UserId != user.Id)
                    result = DivorceResult.NotYourWife;
                else if (!_cache.TryAddDivorceCooldown(user.Id, out remaining))
                {
                    result = DivorceResult.Cooldown;
                }
                else
                {
                    amount = w.Price / 2;

                    if (w.Affinity?.UserId == user.Id)
                    {
                        await _cs.AddAsync(w.Waifu.UserId, "Waifu Compensation", amount, gamble: true);
                        w.Price = (int)Math.Floor(w.Price * 0.75f);
                        result = DivorceResult.SucessWithPenalty;
                    }
                    else
                    {
                        await _cs.AddAsync(user.Id, "Waifu Refund", amount, gamble: true);

                        result = DivorceResult.Success;
                    }
                    var oldClaimer = w.Claimer;
                    w.Claimer = null;

                    uow._context.WaifuUpdates.Add(new WaifuUpdate()
                    {
                        User = w.Waifu,
                        Old = oldClaimer,
                        New = null,
                        UpdateType = WaifuUpdateType.Claimed
                    });
                }

                await uow.CompleteAsync();
            }

            return (w, result, amount, remaining);
        }

        public async Task<bool> GiftWaifuAsync(ulong from, int count, IUser giftedWaifu, WaifuItem itemObj)
        {
            itemObj.Count = count;

            if (!await _cs.RemoveAsync(from, "Bought waifu item", itemObj.Price*count, gamble: true))
            {
                return false;
            }

            using (var uow = _db.UnitOfWork)
            {
                itemObj.GifterWaifuInfoId = uow.Waifus.ByWaifuUserId(from).Id;

                var w = uow.Waifus.ByWaifuUserId(giftedWaifu.Id, set => set.Include(x => x.Items)
                    .Include(x => x.Claimer));
                if (w == null)
                {
                    uow.Waifus.Add(w = new WaifuInfo()
                    {
                        Affinity = null,
                        Claimer = null,
                        Price = 1,
                        Waifu = uow.DiscordUsers.GetOrCreate(giftedWaifu),
                    });
                }

                w.Items.Add(itemObj);

                if (w.Claimer?.UserId == from)
                {
                    w.Price += (int)(itemObj.Price * 0.95) * count;
                }
                else
                    w.Price += (itemObj.Price / 2) * count;

                await uow.CompleteAsync();
            }
            return true;
        }

        public WaifuInfoStats GetFullWaifuInfoAsync(IGuildUser target)
        {
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(target);
                var wi = uow.Waifus.GetWaifuInfo(target.Id);
                var name = target.Username;
                if (!string.IsNullOrWhiteSpace(target.Nickname))
                    name = target.Nickname;

                if (wi == null)
                {
                    wi = new WaifuInfoStats
                    {
                        AffinityCount = 0,
                        AffinityName = null,
                        ClaimCount = 0,
                        ClaimerName = null,
                        Claims30 = new List<string>(),
                        DivorceCount = 0,
                        FullName = name,
                        Items = new List<WaifuItem>(),
                        Immune = false,
                        Reputation = 0,
                        Price = 1,
                        Info = null
                    };
                }

                return wi;
            }
        }

        public ClubInfo GetClubName(IGuildUser target)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.Clubs.GetByMember(target.Id);
            }
        }

        public WaifuInfoStats GetCountInfoAsync(IUser target)
        {
            using (var uow = _db.UnitOfWork)
            {
                var wi = uow.Waifus.GetWaifuInfo(target.Id);
                if (wi == null)
                {
                    wi = new WaifuInfoStats
                    {
                        ClaimCount = 0,
                    };
                }

                return wi;
            }
        }

        public string GetRepTitle(int count)
        {
            ClaimTitle title;
            if (count < 10)
                title = ClaimTitle.Status0;
            else if (count < 30)
                title = ClaimTitle.Status1;
            else if (count < 50)
                title = ClaimTitle.Status2;
            else if (count < 70)
                title = ClaimTitle.Status3;
            else if (count < 100)
                title = ClaimTitle.Status4;
            else if (count < 130)
                title = ClaimTitle.Status5;
            else if (count < 150)
                title = ClaimTitle.Status6;
            else if (count < 200)
                title = ClaimTitle.Status7;
            else if (count < 300)
                title = ClaimTitle.Status8;
            else if (count < 350)
                title = ClaimTitle.Status9;
            else if (count < 400)
                title = ClaimTitle.Status10;
            else if (count < 500)
                title = ClaimTitle.Status11;
            else if (count < 600)
                title = ClaimTitle.Status12;
            else if (count < 700)
                title = ClaimTitle.Status13;
            else if (count < 750)
                title = ClaimTitle.Status14;
            else if (count < 800)
                title = ClaimTitle.Status15;
            else if (count < 900)
                title = ClaimTitle.Status16;
            else if (count < 1000)
                title = ClaimTitle.Status17;
            else if (count < 1100)
                title = ClaimTitle.Status18;
            else if (count < 1200)
                title = ClaimTitle.Status19;
            else if (count < 1250)
                title = ClaimTitle.Status20;
            else if (count < 1300)
                title = ClaimTitle.Status21;
            else if (count < 1400)
                title = ClaimTitle.Status22;
            else if (count < 1500)
                title = ClaimTitle.Status23;
            else if (count < 1550)
                title = ClaimTitle.Status24;
            else if (count < 1700)
                title = ClaimTitle.Status25;
            else if (count < 1750)
                title = ClaimTitle.Status26;
            else if (count < 1800)
                title = ClaimTitle.Status27;
            else if (count < 1900)
                title = ClaimTitle.Status28;
            else if (count < 2000)
                title = ClaimTitle.Status29;
            else if (count < 2050)
                title = ClaimTitle.Status30;
            else if (count < 2100)
                title = ClaimTitle.Status31;
            else if (count < 2200)
                title = ClaimTitle.Status32;
            else if (count < 2300)
                title = ClaimTitle.Status33;
            else if (count < 2400)
                title = ClaimTitle.Status34;
            else if (count < 2500)
                title = ClaimTitle.Status35;
            else if (count < 2600)
                title = ClaimTitle.Status36;
            else if (count < 2700)
                title = ClaimTitle.Status37;
            else if (count < 2800)
                title = ClaimTitle.Status38;
            else if (count < 2900)
                title = ClaimTitle.Status39;
            else if (count < 3000)
                title = ClaimTitle.Status40;
            else if (count < 3500)
                title = ClaimTitle.Status41;
            else if (count < 3600)
                title = ClaimTitle.Status42;
            else if (count < 3800)
                title = ClaimTitle.Status43;
            else if (count < 4000)
                title = ClaimTitle.Status44;
            else
                title = ClaimTitle.Status45;

            return title.ToString().Replace('_', ' ');
        }

        public string GetAffinityTitle(int count)
        {
            AffinityTitle title;
            if (count < 1)
                title = AffinityTitle.Pure;
            else if (count < 2)
                title = AffinityTitle.Faithful;
            else if (count < 3)
                title = AffinityTitle.Defiled;
            else if (count < 4)
                title = AffinityTitle.Cheater;
            else if (count < 5)
                title = AffinityTitle.Tainted;
            else if (count < 6)
                title = AffinityTitle.Corrupted;
            else if (count < 7)
                title = AffinityTitle.Lewd;
            else if (count < 8)
                title = AffinityTitle.Sloot;
            else if (count < 9)
                title = AffinityTitle.Depraved;
            else
                title = AffinityTitle.Harlot;

            return title.ToString().Replace('_', ' ');
        }
    }
}

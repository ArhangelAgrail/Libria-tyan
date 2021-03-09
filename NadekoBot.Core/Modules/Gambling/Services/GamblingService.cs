using Discord;
using Discord.WebSocket;
using NadekoBot.Core.Modules.Gambling.Common;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Modules.Gambling.Common.Connect4;
using NadekoBot.Modules.Gambling.Common.WheelOfFortune;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NadekoBot.Modules.Xp.Common;

namespace NadekoBot.Modules.Gambling.Services
{
    public class GamblingService : INService
    {
        private readonly DbService _db;
        private readonly ICurrencyService _cs;
        private readonly IBotConfigProvider _bc;
        private readonly NadekoBot _bot;
        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        private readonly IDataCache _cache;

        public ConcurrentDictionary<(ulong, ulong), RollDuelGame> Duels { get; } = new ConcurrentDictionary<(ulong, ulong), RollDuelGame>();
        public ConcurrentDictionary<ulong, Connect4Game> Connect4Games { get; } = new ConcurrentDictionary<ulong, Connect4Game>();

        private readonly Timer _decayTimer;

        public GamblingService(DbService db, NadekoBot bot, ICurrencyService cs, IBotConfigProvider bc,
            DiscordSocketClient client, IDataCache cache)
        {
            _db = db;
            _cs = cs;
            _bc = bc;
            _bot = bot;
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _cache = cache;

            if (_bot.Client.ShardId == 0)
            {
                _decayTimer = new Timer(_ =>
                {
                    var decay = _bc.BotConfig.DailyCurrencyDecay;
                    if (decay <= 0)
                        return;

                    using (var uow = _db.UnitOfWork)
                    {
                        var botc = uow.BotConfig.GetOrCreate();
                        //once every 24 hours
                        if (DateTime.UtcNow - _bc.BotConfig.LastCurrencyDecay < TimeSpan.FromHours(24))
                            return;
                        uow.DiscordUsers.CurrencyDecay(decay, _bot.Client.CurrentUser.Id);
                        _cs.AddAsync(_bot.Client.CurrentUser.Id,
                            "Currency Decay",
                            uow.DiscordUsers.GetCurrencyDecayAmount(decay));
                        _bc.BotConfig.LastCurrencyDecay = botc.LastCurrencyDecay = DateTime.UtcNow;
                        uow.Complete();
                    }
                }, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            }

            //using (var uow = _db.UnitOfWork)
            //{
            //    //refund all of the currency users had at stake in gambling games
            //    //at the time bot was restarted

            //    var stakes = uow._context.Set<Stake>()
            //        .ToArray();

            //    var userIds = stakes.Select(x => x.UserId).ToArray();
            //    var reasons = stakes.Select(x => "Stake-" + x.Source).ToArray();
            //    var amounts = stakes.Select(x => x.Amount).ToArray();

            //    _cs.AddBulkAsync(userIds, reasons, amounts, gamble: true).ConfigureAwait(false);

            //    foreach (var s in stakes)
            //    {
            //        _cs.AddAsync(s.UserId, "Stake-" + s.Source, s.Amount, gamble: true)
            //            .GetAwaiter()
            //            .GetResult();
            //    }

            //    uow._context.Set<Stake>().RemoveRange(stakes);
            //    uow.Complete();
            //    _log.Info("Refunded {0} users' stakes.", stakes.Length);
            //}
        }

        public struct EconomyResult
        {
            public decimal Cash { get; set; }
            public decimal Planted { get; set; }
            public decimal Waifus { get; set; }
            public decimal OnePercent { get; set; }
            public long Bot { get; set; }
        }

        public EconomyResult GetEconomy()
        {
            if (_cache.TryGetEconomy(out var data))
            {
                try
                {
                    return JsonConvert.DeserializeObject<EconomyResult>(data);
                }
                catch { }
            }

            decimal cash;
            decimal onePercent;
            decimal planted;
            decimal waifus;
            long bot;

            using (var uow = _db.UnitOfWork)
            {
                cash = uow.DiscordUsers.GetTotalCurrency();
                onePercent = uow.DiscordUsers.GetTopOnePercentCurrency(_client.CurrentUser.Id);
                planted = uow.PlantedCurrency.GetTotalPlanted();
                waifus = uow.Waifus.GetTotalValue();
                bot = uow.DiscordUsers.GetUserCurrency(_client.CurrentUser.Id);
                cash = cash - bot;
            }

            var result = new EconomyResult
            {
                Cash = cash,
                Planted = planted,
                Bot = bot,
                Waifus = waifus,
                OnePercent = onePercent,
            };

            _cache.SetEconomy(JsonConvert.SerializeObject(result));
            return result;
        }

        public Achievements[] AllAchievements()
        {
            Achievements[] allAchieves;
            using (var uow = _db.UnitOfWork)
            {
                allAchieves = uow.Achievements.GetAllAchievements();
                uow.Complete();
            }
            return allAchieves;
        }

        public Task<WheelOfFortuneGame.Result> WheelOfFortuneSpinAsync(ulong userId, long bet)
        {
            return new WheelOfFortuneGame(userId, bet, _cs).SpinAsync();
        }

        public async Task<int> GiveReputation(IUser target, IUser user)
        {
            var total = 1;
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);

                var w = uow.Waifus.ByWaifuUserId(target.Id);
                var u = uow.Waifus.ByWaifuUserId(user.Id);
                var thisUser = uow.DiscordUsers.GetOrCreate(target);

                if (u == null)
                {
                    uow.Waifus.Add(new WaifuInfo()
                    {
                        Waifu = thisUser,
                        Price = 1,
                        Claimer = null,
                        Immune = false,
                        Reputation = 1,
                        LastReputation = 0
                    });
                }

                if (w == null)
                {
                    uow.Waifus.Add(new WaifuInfo()
                    {
                        Waifu = thisUser,
                        Price = 1,
                        Claimer = null,
                        Immune = false,
                        Reputation = 1
                    });
                }
                else
                {
                    int rep = w.Reputation;
                    rep += 1;
                    w.Reputation = rep;
                    total = w.Reputation;
                }

                u.LastReputation = target.Id;

                var guildUser = target as IGuildUser;
                var roles = uow.Achievements.ByGroup("Reputation");
                var roleIds = roles.Select(x => x.RoleId).ToArray();
                var sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                IRole role = null;
                foreach (var cond in roles)
                {
                    if (total >= cond.Condition)
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
                                await guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                await Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                if (role != null)
                {
                    try
                    {
                        await guildUser.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch
                    { }
                }

                roles = uow.Achievements.ByGroup("ReputationGet");
                roleIds = roles.Select(x => x.RoleId).ToArray();
                sameRoles = guildUser.RoleIds
                    .Where(r => roleIds.Contains(r));

                role = null;
                foreach (var cond in roles)
                {
                    if (GetRepLogForUser(target).Count() >= cond.Condition)
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
                                await guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                                await Task.Delay(50).ConfigureAwait(false);
                            }
                            catch
                            { }
                        }
                }

                if (role != null)
                {
                    try
                    {
                        await guildUser.AddRoleAsync(role).ConfigureAwait(false);
                    }
                    catch
                    { }
                }

                await uow.CompleteAsync();
            }

            return total;
        }

        public async Task<bool> LogReputation(IUser target, IUser user)
        {
            var result = false;
            using (var uow = _db.UnitOfWork)
            {
                var thisUser = uow.DiscordUsers.GetOrCreate(target);
                uow.RepLog.Add(new RepLog()
                {
                    UserId = thisUser.UserId,
                    FromId = user.Id
                });

                await uow.CompleteAsync();
                result = true;
            }

            return result;
        }

        public RepLogResult[] GetRepLogForUser(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.RepLog.GetForUser(user.Id);
            }
        }

        public RepLogResult[] GetRepLogByUser(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.RepLog.GetByUser(user.Id);
            }
        }

        public EventSchedule[] EventsList(ulong gid, DateTime date)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.EventSchedule.ByDate(gid, date);
            }
        }

        public async Task<bool> AddEvent(IGuild guild, IUser user, string desc, DateTime date)
        {
            var result = false;
            using (var uow = _db.UnitOfWork)
            {
                var thisUser = uow.DiscordUsers.GetOrCreate(user);
                uow.EventSchedule.Add(new EventSchedule()
                {
                    GuildId = guild.Id,
                    UserId = thisUser.UserId,
                    Date = date,
                    Type = "0",
                    Description = desc
                }) ;

                await uow.CompleteAsync();
                result = true;
            }

            return result;
        }


        public int GetUserLevel(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);
                var lvl = new LevelStats(du.TotalXp).Level;
                return lvl;
            }
        }

        public long GetUserCurrency(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(user);
                var cur = du.CurrencyAmount;
                return cur;
            }
        }

        public ulong GetLastReputation(IUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var lastrep = uow.Waifus.GetWaifuInfo(user.Id).LastReputation;
                return lastrep;
            }
        }
    }
}

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;
using NadekoBot.Core.Common;
using NadekoBot.Core.Modules.Gambling.Common;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Extensions;
using NadekoBot.Modules.Gambling.Services;
using NadekoBot.Modules.Xp.Common;
using Remotion.Linq.Clauses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Gambling
{
    public partial class Gambling : GamblingTopLevelModule<GamblingService>
    {
        private readonly DbService _db;
        private readonly ICurrencyService _cs;
        private readonly IDataCache _cache;
        private readonly DiscordSocketClient _client;
        private readonly IBotConfigProvider _bc;

        private string CurrencyName => Bc.BotConfig.CurrencyName;
        private string CurrencyPluralName => Bc.BotConfig.CurrencyPluralName;
        private string CurrencySign => Bc.BotConfig.CurrencySign;

        public Gambling(DbService db, ICurrencyService currency,
            IDataCache cache, DiscordSocketClient client, IBotConfigProvider bc)
        {
            _db = db;
            _cs = currency;
            _cache = cache;
            _client = client;
            _bc = bc;
        }

        public long GetCurrency(ulong id)
        {
            using (var uow = _db.UnitOfWork)
            {
                return uow.DiscordUsers.GetUserCurrency(id);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Achievements()
        {
            var Achieves = _service.AllAchievements();

            await Context.SendPaginatedConfirmAsync(0, (page) =>
            {
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithAuthor(name: GetText("all_achievements"))
                    .WithDescription(string.Join("\n", Achieves
                    .Skip(page * 50)
                    .Take(50)
                    .GroupBy(x => x.GroupName)
                    .Select(x => $"{Format.Bold(GetText("achieve_" + x.Key))}:\n{string.Join("\n", x.Select(y => $"☆{y.Condition}+ <@&{y.RoleId}>"))}")));

                return embed;
            }, 100, 20, false).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Economy()
        {
            var ec = _service.GetEconomy();
            decimal onePercent = 0;
            if (ec.Cash > 0)
            {
                onePercent = ec.OnePercent / ec.Cash;
            }
            var embed = new EmbedBuilder()
                .WithTitle(GetText("economy_state"))
                .AddField(GetText("currency_owned"), ((BigInteger)ec.Cash) + _bc.BotConfig.CurrencySign)
                .AddField(GetText("currency_one_percent"), (onePercent * 100).ToString("F2") + "%")
                .AddField(GetText("currency_planted"), ((BigInteger)ec.Planted) + _bc.BotConfig.CurrencySign)
                .AddField(GetText("owned_waifus_total"), ((BigInteger)ec.Waifus) + _bc.BotConfig.CurrencySign)
                .AddField(GetText("bot_currency"), ec.Bot + _bc.BotConfig.CurrencySign)
                .AddField(GetText("total"), ((BigInteger)(ec.Cash + ec.Bot + ec.Planted + ec.Waifus)) + _bc.BotConfig.CurrencySign)
                .WithOkColor();

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Rep([Remainder]IUser target)
        {
            var period = Bc.BotConfig.TimelyCurrencyPeriod;

            if (Context.User.Id != target.Id)
            {
                if (_service.GetLastReputation(Context.User) != target.Id)
                {
                    if (_service.GetUserLevel(Context.User) < Bc.BotConfig.MinimumLevel)
                    {
                        await ReplyErrorLocalized("lvl_rep", Bc.BotConfig.MinimumLevel).ConfigureAwait(false);
                        return;
                    }

                    TimeSpan? rem;
                    if ((rem = _cache.AddRepGive(Context.User.Id, period)) != null)
                    {
                        await ReplyErrorLocalized("rep_already_gived", rem?.Days, rem?.Hours, rem?.Minutes, rem?.Seconds).ConfigureAwait(false);
                        return;
                    }

                    var (total, curAchievement, repForNext, rolePercent) = await _service.GiveReputation(target, Context.User);
                    var achievementStr = curAchievement is null ? 
                        GetText("cur_achievement_null", repForNext) : repForNext > 0 ? 
                        GetText("cur_achievement", curAchievement.Mention, String.Format("{0:0.##}", rolePercent*100), repForNext) : GetText("cur_achievement_max", curAchievement.Mention, String.Format("{0:0.##}", rolePercent*100));

                    await _service.LogReputation(target, Context.User);
                    await Context.Channel.SendConfirmAsync(GetText("rep", Context.User.Mention, target.Mention, achievementStr, period), GetText("total_rep", total)).ConfigureAwait(false);
                }
                else
                await ReplyErrorLocalized("last_rep", target.Mention).ConfigureAwait(false);
            }
            else
                await ReplyErrorLocalized("self_rep").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        public async Task RepReset()
        {
            _cache.RemoveAllRepGives();
            await ReplyConfirmLocalized("rep_reset").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(0)]
        public Task ReputationLog()
            => ReputationLog(0, Context.User);

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(1)]
        public Task ReputationLog(int type)
            => ReputationLog(type, Context.User);

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(2)]
        [OwnerOnly]
        public async Task ReputationLog(int type, [Remainder]IUser user)
        {
            if (type == 0)
            {
                var replog = _service.GetRepLogForUser(user);

                await Context.SendPaginatedConfirmAsync(0, (page) =>
                {
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithFooter(GetText("page", page + 1))
                    .WithAuthor(name: GetText("rep_for_user", user.ToString()), iconUrl: user.GetAvatarUrl())
                    .WithDescription(string.Join("\n", replog.Where(x => x.UserId == 0 || x.UserId == 1).Select(x =>
                        {
                            if (x.UserId == 0)
                                return $"{Format.Bold(GetText("warn_take"))}: **-{x.Count * 100}**☆";
                            if (x.UserId == 1)
                                return $"{Format.Bold(GetText("warn_clear"))}: **+{x.Count * 100}**☆";
                            return "";
                        })) + "\n" + string.Join("\n", replog
                        .OrderByDescending(x => x.Count)
                        .Skip(page * 20)
                        .Take(20)
                        .Where(x => x.UserId != 0 && x.UserId != 1)
                        .Select(x =>
                        {
                            return $"<@{x.UserId}>: **+{x.Count}**☆";
                        })));

                    return embed;
                }, 100, 20, false).ConfigureAwait(false);
            }
            else
            {
                var replog = _service.GetRepLogByUser(user);

                await Context.SendPaginatedConfirmAsync(0, (page) =>
                {
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithFooter(GetText("page", page + 1))
                        .WithAuthor(name: GetText("rep_by_user", user.ToString()), iconUrl: user.GetAvatarUrl())
                        .WithDescription(string.Join("\n", replog
                        .OrderByDescending(x => x.Count)
                        .Skip(page * 20)
                        .Take(20)
                        .Select(x =>
                        {
                            return $"<@{x.UserId}>: **+{x.Count}**☆";
                        })));

                    return embed;
                }, 100, 20, false).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Timely()
        {
            var val = Bc.BotConfig.TimelyCurrency;
            var period = Bc.BotConfig.TimelyCurrencyPeriod;
            if (val <= 0 || period <= 0)
            {
                await ReplyErrorLocalized("timely_none").ConfigureAwait(false);
                return;
            }

            TimeSpan? rem;
            if ((rem = _cache.AddTimelyClaim(Context.User.Id, period)) != null)
            {
                await ReplyErrorLocalized("timely_already_claimed", rem?.Days, rem?.Hours, rem?.Minutes, rem?.Seconds).ConfigureAwait(false);
                return;
            }

            var bonus = _service.GetBonusValue();
            string strbonus = GetText("role_bonus");

            foreach (var bon in bonus)
            {
                var user = Context.User as IGuildUser;
                if (user.RoleIds.Contains(bon.RoleId))
                {
                    val += bon.Bonus;
                    strbonus += $"\n<@&{bon.RoleId}> : **+{bon.Bonus}**{Bc.BotConfig.CurrencySign}";
                }
            }

            await _cs.AddAsync(Context.User.Id, "Timely claim", val, gamble:true).ConfigureAwait(false);
            var cur = _service.GetUserCurrency(Context.User);

            string usercur = GetText("currency_left", String.Format("{0:#,0}", cur), Bc.BotConfig.CurrencySign);
            await Context.Channel.SendConfirmAsync(GetText("timely", Context.User.Mention, String.Format("{0:#,0}", val) + Bc.BotConfig.CurrencySign, period, Bc.BotConfig.TimelyCurrency + Bc.BotConfig.CurrencySign, strbonus), usercur).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        public async Task TimelyReset()
        {
            _cache.RemoveAllTimelyClaims();
            await ReplyConfirmLocalized("timely_reset").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        public async Task TimelySet(int num, int period = 24)
        {
            if (num < 0 || period < 0)
                return;
            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate(set => set);
                _bc.BotConfig.TimelyCurrency = bc.TimelyCurrency = num;
                _bc.BotConfig.TimelyCurrencyPeriod = bc.TimelyCurrencyPeriod = period;
                uow.Complete();
            }
            if (num == 0)
                await ReplyConfirmLocalized("timely_set_none").ConfigureAwait(false);
            else
                await ReplyConfirmLocalized("timely_set", Format.Bold(num + Bc.BotConfig.CurrencySign), Format.Bold(period.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Raffle([Remainder] IRole role = null)
        {
            role = role ?? Context.Guild.EveryoneRole;

            var members = (await role.GetMembersAsync().ConfigureAwait(false)).Where(u => u.Status != UserStatus.Offline);
            var membersArray = members as IUser[] ?? members.ToArray();
            if (membersArray.Length == 0)
            {
                return;
            }
            var usr = membersArray[new NadekoRandom().Next(0, membersArray.Length)];
            await Context.Channel.SendConfirmAsync("🎟 " + GetText("raffled_user"), $"**{usr.Username}#{usr.Discriminator}**", footer: $"ID: {usr.Id}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task RaffleAny([Remainder] IRole role = null)
        {
            role = role ?? Context.Guild.EveryoneRole;

            var members = (await role.GetMembersAsync().ConfigureAwait(false));
            var membersArray = members as IUser[] ?? members.ToArray();
            if (membersArray.Length == 0)
            {
                return;
            }
            var usr = membersArray[new NadekoRandom().Next(0, membersArray.Length)];
            await Context.Channel.SendConfirmAsync("🎟 " + GetText("raffled_user"), $"**{usr.Username}#{usr.Discriminator}**", footer: $"ID: {usr.Id}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task RaffleAward()
        {
            var everyoneRole = Context.Guild.EveryoneRole;
            var sponsorRole = Context.Guild.GetRole(761300230842744902);
            var sponsorPlusRole = Context.Guild.GetRole(927134254318641152);
            var boosterRole = Context.Guild.GetRole(585568500303527947);
            var patreonRole = Context.Guild.GetRole(319081760241745920);

            var everyoneMembers = await everyoneRole.GetMembersAsync().ConfigureAwait(false);
            var sponsorMembers = await sponsorRole.GetMembersAsync().ConfigureAwait(false);
            sponsorMembers = sponsorMembers.Union(await sponsorPlusRole.GetMembersAsync().ConfigureAwait(false));
            var boosterMembers = await boosterRole.GetMembersAsync().ConfigureAwait(false);
            var patreonMembers = await patreonRole.GetMembersAsync().ConfigureAwait(false);

            var everyoneMembersArray = everyoneMembers as IUser[] ?? everyoneMembers.ToArray();
            var sponsorMembersArray = sponsorMembers as IUser[] ?? sponsorMembers.ToArray();
            var boosterMembersArray = boosterMembers as IUser[] ?? boosterMembers.ToArray();
            var patreonMembersArray = patreonMembers as IUser[] ?? patreonMembers.ToArray();

            /* everyone raffle */
            var userE = everyoneMembersArray[0];
        reroll:
            userE = everyoneMembersArray[new NadekoRandom().Next(0, everyoneMembersArray.Length)];

            var userG = userE as SocketGuildUser;
            if (userG.Roles.Count == 1) goto reroll;

            var userElvl = _service.GetUserLevel(userE);
            var amountE = userElvl * 100;
            amountE = amountE < 1000 ? 1000 : amountE;
            await _cs.AddAsync(userE.Id, $"Awarded by everyone raffle.", amountE, gamble: true);

            /* sponsor raffle */
            var userS = sponsorMembersArray[new NadekoRandom().Next(0, sponsorMembersArray.Length)];

            var userSlvl = _service.GetUserLevel(userS);
            var amountS = userSlvl * 100;
            amountS = amountS < 1000 ? 1000 : amountS;
            await _cs.AddAsync(userS.Id, $"Awarded by sponsor raffle.", amountS, gamble: true);

            /* booster raffle */
            var userB = boosterMembersArray[new NadekoRandom().Next(0, boosterMembersArray.Length)];

            var userBlvl = _service.GetUserLevel(userB);
            var amountB = userBlvl * 100;
            amountB = amountB < 1000 ? 1000 : amountB;
            await _cs.AddAsync(userB.Id, $"Awarded by booster raffle.", amountB, gamble: true);

            /* patreon raffle */
            var userP = patreonMembersArray[new NadekoRandom().Next(0, patreonMembersArray.Length)];

            var userPlvl = _service.GetUserLevel(userP);
            var amountP = userPlvl * 100;
            amountP = amountP < 1000 ? 1000 : amountP;
            await _cs.AddAsync(userP.Id, $"Awarded by patreon raffle.", amountP, gamble: true);


            await Context.Channel.SendMessageAsync(GetText("raffled_grats", userE.Mention, userS.Mention, userB.Mention, userP.Mention));
            await Context.Channel.SendConfirmAsync("🎟 " + GetText("raffled_award"),
                $"{GetText("raffled_resultE", userE.Mention, Format.Bold($"{String.Format("{0:#,0}", amountE)}{_bc.BotConfig.CurrencySign}"), userElvl, everyoneMembers.Count())}" +
                $"\n\n{GetText("raffled_resultS", userS.Mention, Format.Bold($"{String.Format("{0:#,0}", amountS)}{_bc.BotConfig.CurrencySign}"), userSlvl, sponsorMembers.Count())}" +
                $"\n\n{GetText("raffled_resultB", userB.Mention, Format.Bold($"{String.Format("{0:#,0}", amountB)}{_bc.BotConfig.CurrencySign}"), userBlvl, boosterMembers.Count())}" +
                $"\n\n{GetText("raffled_resultP", userP.Mention, Format.Bold($"{String.Format("{0:#,0}", amountP)}{_bc.BotConfig.CurrencySign}"), userPlvl, patreonMembers.Count())}", footer: $"{GetText("raffled_desc")}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(1)]
        public async Task Cash([Remainder] IUser user = null)
        {
            user = user ?? Context.User;
            await ConfirmLocalized("has", user.Mention, Format.Bold($"{String.Format("{0:#,0}", GetCurrency(user.Id))} {CurrencySign}")).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(2)]
        public Task CurrencyTransactions(int page = 1) =>
            InternalCurrencyTransactions(Context.User.Id, page);

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        [Priority(0)]
        public Task CurrencyTransactions([Remainder] IUser usr) =>
            InternalCurrencyTransactions(usr.Id, 1);

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        [Priority(1)]
        public Task CurrencyTransactions(IUser usr, int page) =>
            InternalCurrencyTransactions(usr.Id, page);

        private Task InternalCurrencyTransactions(ulong userId, int page)
        {

            if (--page < 0 || page > 125)
                return Task.CompletedTask;

            return Context.SendPaginatedConfirmAsync(page, (curPage) =>
            {
                var trs = new List<CurrencyTransaction>();
                using (var uow = _db.UnitOfWork)
                {
                    trs = uow.CurrencyTransactions.GetPageFor(userId, curPage);
                }

                var embed = new EmbedBuilder()
                    .WithTitle(GetText("transactions",
                    ((SocketGuild)Context.Guild)?.GetUser(userId)?.ToString() ?? $"{userId}"))
                    .WithFooter(GetText("page", curPage + 1))
                    .WithOkColor();

                if (!trs.Any())
                    return embed.WithDescription("-");
                else
                {
                    var desc = "";
                    foreach (var tr in trs)
                    {
                        var type = tr.Amount > 0 ? "🔵" : "🔴";
                        var date = Format.Code($"〖{tr.DateAdded:HH:mm yyyy-MM-dd}〗");
                        desc += $"\\{type} {date} {Format.Bold(tr.Amount.ToString())}\n\t{tr.Reason?.Trim()}\n";
                    }
                    return embed.WithDescription(desc);
                }
            }, 1000, 10, addPaginatedFooter: false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(0)]
        public async Task Cash(ulong userId)
        {
            await ReplyConfirmLocalized("has", Format.Code(userId.ToString()), $"{GetCurrency(userId)} {CurrencySign}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task Give(ShmartNumber amount, IGuildUser receiver, [Remainder] string msg = null)
        {
            if (amount <= 0 || Context.User.Id == receiver.Id || receiver.IsBot)
                return;

            using (var uow = _db.UnitOfWork)
            {
                var du = uow.DiscordUsers.GetOrCreate(Context.User);

                if (new LevelStats(du.TotalXp).Level < Bc.BotConfig.MinimumLevel)
                {
                    await ReplyErrorLocalized("lvl_give", Bc.BotConfig.MinimumLevel).ConfigureAwait(false);
                    return;
                }

                var inUse = uow.CurrencyTransactions.GetClubAwarded(Context.User.Id);
                if (inUse > 0)
                {
                    await ReplyErrorLocalized("should_use", Format.Bold(String.Format("{0:#,0}", inUse.ToString())), Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                    return;
                }
            }

            var success = await _cs.RemoveAsync((IGuildUser)Context.User, $"Gift to {receiver.Username} ({receiver.Id}).", amount, false).ConfigureAwait(false);
            if (!success)
            {
                await ReplyErrorLocalized("not_enough", CurrencyPluralName).ConfigureAwait(false);
                return;
            }
            await _cs.AddAsync(receiver, $"Gift from {Context.User.Username} ({Context.User.Id}) - {msg}.", amount, false).ConfigureAwait(false);
            
            try
            {
                await (await receiver.GetOrCreateDMChannelAsync())
                    .EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(GetText("received_cur", Bc.BotConfig.CurrencySign, Context.Guild.Name))
                        .WithDescription(GetText("received_cur_description", Format.Bold(Context.User.Username), Format.Bold(amount.ToString()), Bc.BotConfig.CurrencySign, Format.Italics(msg))));
            }
            catch
            { }

            await ReplyConfirmLocalized("gifted", amount + CurrencySign, Format.Bold(receiver.Mention), msg).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task InvestClub(ShmartNumber amount)
        {
            if (amount <= 0)
                return;
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByMember(Context.User.Id);
                if (club == null)
                {
                    await ReplyErrorLocalized("no_club").ConfigureAwait(false);
                    return;
                }

                var inUse = uow.CurrencyTransactions.GetClubAwarded(Context.User.Id);
                if (inUse > 0)
                {
                    await ReplyErrorLocalized("should_use", Format.Bold(String.Format("{0:#,0}", inUse.ToString())), Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                    return;
                }

                var success = await _cs.RemoveAsync((IGuildUser)Context.User, $"Invest into {club.Name} storage.", amount, false, gamble: true).ConfigureAwait(false);
                if (!success)
                {
                    await ReplyErrorLocalized("not_enough", CurrencyPluralName).ConfigureAwait(false);
                    return;
                }

                var user = uow.DiscordUsers.GetOrCreate(Context.User);
                user.ClubInvetsAmount += (int)amount;
                club.Currency += (int)amount;
                club.TotalCurrency += (int)amount;
                await uow.CompleteAsync();

                string cur = GetText("currency_left", String.Format("{0:#,0}", _service.GetUserCurrency(Context.User)), Bc.BotConfig.CurrencySign);
                await Context.Channel.SendConfirmAsync(GetText("club_invested", Format.Bold(amount.ToString()) + CurrencySign, Format.Bold(club.Name)), cur).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task ClubAward(int amount, IGuildUser user)
        {
            using (var uow = _db.UnitOfWork)
            {
                var club = uow.Clubs.GetByMember(Context.User.Id);
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

                var usr = club.Users.FirstOrDefault(x => x.ToString().ToUpperInvariant() == user.Username.ToUpperInvariant() + "#" + user.Discriminator);
                if (usr == null)
                {
                    await ReplyErrorLocalized("club_user_not_found");
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

                if (club.textId == 0)
                {
                    await ReplyErrorLocalized("club_text_not_exists").ConfigureAwait(false);
                    return;
                }

                if (club.Currency < amount)
                {
                    await ReplyErrorLocalized("club_not_enough").ConfigureAwait(false);
                    return;
                }

                TimeSpan? rem;
                if ((rem = _cache.AddClubAward(Context.User.Id, 24)) != null)
                {
                    await ReplyErrorLocalized("club_already_awarded", rem?.Days, rem?.Hours, rem?.Minutes, rem?.Seconds).ConfigureAwait(false);
                    return;
                }

                await _cs.AddAsync(user.Id, "Club Award", amount, gamble: true).ConfigureAwait(false);
                club.Currency -= amount;

                await Context.Channel.SendConfirmAsync(GetText("club_awarded", user.Mention, Format.Bold(amount.ToString()), Bc.BotConfig.CurrencySign, Format.Bold(club.Name)),
                    GetText("club_currency_left", club.Currency, Bc.BotConfig.CurrencySign)).ConfigureAwait(false);

                await uow.CompleteAsync();
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public Task Give(ShmartNumber amount, [Remainder] IGuildUser receiver)
            => Give(amount, receiver, null);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(0)]
        public Task Award(ShmartNumber amount, [Remainder] IGuildUser usr) =>
            Award(amount, usr);

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        [Priority(1)]
        public async Task Award(ShmartNumber amount, IGuildUser usr, [Remainder] string msg = null)
        {
            if (amount <= 0)
                return;

            await _cs.AddAsync(usr.Id,
                $"Awarded by bot owner. ({Context.User.Username}/{Context.User.Id}) {(msg ?? "")}",
                amount,
                gamble: (Context.Client.CurrentUser.Id != usr.Id)).ConfigureAwait(false);

            try
            {
                await (await usr.GetOrCreateDMChannelAsync())
                    .EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(GetText("award_cur", Bc.BotConfig.CurrencySign, Context.Guild.Name))
                        .WithDescription(GetText("award_cur_description", Format.Bold(amount.ToString()), Bc.BotConfig.CurrencySign, Format.Italics(msg))));
            }
            catch
            { }

            await ReplyConfirmLocalized("awarded", amount + CurrencySign, $"<@{usr.Id}>").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(2)]
        public async Task Award(ShmartNumber amount, [Remainder] IRole role)
        {
            var users = (await Context.Guild.GetUsersAsync().ConfigureAwait(false))
                               .Where(u => u.GetRoles().Contains(role))
                               .ToList();

            await _cs.AddBulkAsync(users.Select(x => x.Id),
                users.Select(x => $"Awarded by bot owner to **{role.Name}** role. ({Context.User.Username}/{Context.User.Id})"),
                users.Select(x => amount.Value),
                gamble: true)
                .ConfigureAwait(false);

            await ReplyConfirmLocalized("mass_award",
                amount + CurrencySign,
                Format.Bold(users.Count.ToString()),
                Format.Bold(role.Name)).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task Take(ShmartNumber amount, [Remainder] IGuildUser user)
        {
            if (amount <= 0)
                return;

            if (await _cs.RemoveAsync(user, $"Taken by bot owner.({Context.User.Username}/{Context.User.Id})", amount,
                gamble: (Context.Client.CurrentUser.Id != user.Id)).ConfigureAwait(false))
                await ReplyConfirmLocalized("take", amount + CurrencySign, Format.Bold(user.ToString())).ConfigureAwait(false);
            else
                await ReplyErrorLocalized("take_fail", amount + CurrencySign, Format.Bold(user.ToString()), CurrencyPluralName).ConfigureAwait(false);
        }


        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        public async Task Take(ShmartNumber amount, [Remainder] ulong usrId)
        {
            if (amount <= 0)
                return;

            if (await _cs.RemoveAsync(usrId, $"Taken by bot owner.({Context.User.Username}/{Context.User.Id})", amount,
                gamble: (Context.Client.CurrentUser.Id != usrId)).ConfigureAwait(false))
                await ReplyConfirmLocalized("take", amount + CurrencySign, $"<@{usrId}>").ConfigureAwait(false);
            else
                await ReplyErrorLocalized("take_fail", amount + CurrencySign, Format.Code(usrId.ToString()), CurrencyPluralName).ConfigureAwait(false);
        }

        IUserMessage rdMsg = null;

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task RollDuel(IUser u)
        {
            if (Context.User.Id == u.Id)
                return;

            //since the challenge is created by another user, we need to reverse the ids
            //if it gets removed, means challenge is accepted
            if (_service.Duels.TryRemove((Context.User.Id, u.Id), out var game))
            {
                await game.StartGame().ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task RollDuel(ShmartNumber amount, IUser u)
        {
            if (Context.User.Id == u.Id)
                return;

            if (amount <= 0)
                return;

            var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(GetText("roll_duel"));

            var game = new RollDuelGame(_cs, _client.CurrentUser.Id, Context.User.Id, u.Id, amount);
            //means challenge is just created
            if (_service.Duels.TryGetValue((Context.User.Id, u.Id), out var other))
            {
                if (other.Amount != amount)
                {
                    await ReplyErrorLocalized("roll_duel_already_challenged").ConfigureAwait(false);
                }
                else
                {
                    await RollDuel(u).ConfigureAwait(false);
                }
                return;
            }
            if (_service.Duels.TryAdd((u.Id, Context.User.Id), game))
            {
                game.OnGameTick += Game_OnGameTick;
                game.OnEnded += Game_OnEnded;

                await ReplyConfirmLocalized("roll_duel_challenge",
                    Format.Bold(Context.User.ToString()),
                    Format.Bold(u.ToString()),
                    Format.Bold(amount + CurrencySign))
                        .ConfigureAwait(false);
            }

            async Task Game_OnGameTick(RollDuelGame arg)
            {
                var rolls = arg.Rolls.Last();
                embed.Description += $@"{Format.Bold(Context.User.ToString())} rolled **{rolls.Item1}**
{Format.Bold(u.ToString())} rolled **{rolls.Item2}**
--
";

                if (rdMsg == null)
                {
                    rdMsg = await Context.Channel.EmbedAsync(embed)
                        .ConfigureAwait(false);
                }
                else
                {
                    await rdMsg.ModifyAsync(x =>
                    {
                        x.Embed = embed.Build();
                    }).ConfigureAwait(false);
                }
            }

            async Task Game_OnEnded(RollDuelGame rdGame, RollDuelGame.Reason reason)
            {
                try
                {
                    if (reason == RollDuelGame.Reason.Normal)
                    {
                        var winner = rdGame.Winner == rdGame.P1
                            ? Context.User
                            : u;
                        embed.Description += $"\n**{winner}** Won {((long)(rdGame.Amount * 2 * 0.98)) + CurrencySign}";
                        await rdMsg.ModifyAsync(x => x.Embed = embed.Build())
                            .ConfigureAwait(false);
                    }
                    else if (reason == RollDuelGame.Reason.Timeout)
                    {
                        await ReplyErrorLocalized("roll_duel_timeout").ConfigureAwait(false);
                    }
                    else if (reason == RollDuelGame.Reason.NoFunds)
                    {
                        await ReplyErrorLocalized("roll_duel_no_funds").ConfigureAwait(false);
                    }
                }
                finally
                {
                    _service.Duels.TryRemove((u.Id, Context.User.Id), out var _);
                }
            }
        }

        private async Task InternallBetroll(long amount)
        {
            if (!await CheckBetMandatory(amount).ConfigureAwait(false))
                return;

            if (!await _cs.RemoveAsync(Context.User, "Betroll Gamble", amount, false, gamble: true).ConfigureAwait(false))
            {
                await ReplyErrorLocalized("not_enough", CurrencyPluralName).ConfigureAwait(false);
                return;
            }

            var rnd = new NadekoRandom().Next(0, 101);
            var str = Context.User.Mention + Format.Code(GetText("roll", rnd));
            if (rnd < 67)
            {
                str += "\n" + GetText("better_luck");
            }
            else
            {
                long win;
                if (rnd < 91)
                {
                    win = (long)(amount * Bc.BotConfig.Betroll67Multiplier);
                    str += "\n" + GetText("br_win", win + CurrencySign, 66);
                    await _cs.AddAsync(Context.User, "Betroll Gamble",
                        win, false, gamble: true).ConfigureAwait(false);
                }
                else if (rnd < 100)
                {
                    win = (long)(amount * Bc.BotConfig.Betroll91Multiplier);
                    str += "\n" + GetText("br_win", win + CurrencySign, 90);
                    await _cs.AddAsync(Context.User, "Betroll Gamble",
                        win, false, gamble: true).ConfigureAwait(false);
                }
                else
                {
                    win = (long)(amount * Bc.BotConfig.Betroll100Multiplier);
                    str += "\n" + GetText("br_win", win + CurrencySign, 99) + " 👑";
                    await _cs.AddAsync(Context.User, "Betroll Gamble",
                        win, false, gamble: true).ConfigureAwait(false);
                }
            }

            string cur = GetText("currency_left", String.Format("{0:#,0}", _service.GetUserCurrency(Context.User)), Bc.BotConfig.CurrencySign);
            await Context.Channel.SendConfirmAsync(str, cur).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public Task BetRoll(ShmartNumber amount)
            => InternallBetroll(amount);

        [NadekoCommand, Usage, Description, Aliases]
        public Task Leaderboard(int page = 1)
        {
            if (--page < 0 || page > 100)
                return Task.CompletedTask;

            return Context.SendPaginatedConfirmAsync(page, (curPage) =>
            {
                List<DiscordUser> richest;
                using (var uow = _db.UnitOfWork)
                {
                    richest = uow.DiscordUsers.GetTopRichest(_client.CurrentUser.Id, 9, 9 * curPage).ToList();
                }

                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(CurrencySign + " " + GetText("leaderboard"))
                    .WithFooter(GetText("page", curPage + 1));

                if (!richest.Any())
                    return embed.WithDescription("-");
                else
                {
                    for (var i = 0; i < richest.Count; i++)
                    {
                        var x = richest[i];
                        var usrStr = x.ToString().TrimTo(20, true);

                        embed.AddField(efb => efb.WithName("#" + (i + 1 + curPage * 9) + " " + usrStr)
                                                 .WithValue(String.Format("{0:#,0}", x.CurrencyAmount) + " " + CurrencySign)
                                                 .WithIsInline(true));
                    }
                    return embed;
                }
            }, 1000, 10, addPaginatedFooter: false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Events()
        {
            var Date = DateTime.Now;
            EventSchedule[] Events;
            string Description = "-", today = " (" + GetText("today") + ")";

            var embed = new EmbedBuilder()
                .WithColor(3553599)
                .WithTitle(GetText("events_schedule"))
                .WithDescription(GetText("events_desc"))
                .WithImageUrl("https://media.discordapp.net/attachments/404934630148407297/765968161107214376/PicsArt_10-14-06.56.33.jpg")
                .WithFooter(GetText("events_footer"));

            for (var i = 0; i < 7; i++)
            {
                Events = _service.EventsList(Context.Guild.Id, Date);
                Description = "";
                if (!Events.Any())
                    Description = GetText("no_events");
                else
                    foreach (var n in Events)
                    {
                        Description += GetText("time", n.Date.ToString("HH:mm")) + " - " + n.Description + "\n";
                    }
                if (Date.Day != DateTime.Now.Day)
                    today = "";

                embed.AddField(efb => efb.WithName(":scroll: " + Date.Day + "." + Date.Month + " - " + GetText(Date.DayOfWeek.ToString()) + today)
                                         .WithValue(Description)
                                         .WithIsInline(false));
                Date = Date.AddDays(1);
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task AddEvent(string date, [Remainder] string desc)
        {
            DateTime Date = Convert.ToDateTime(date);

            await _service.AddEvent(Context.Guild, Context.User, desc, Date);
            await ReplyConfirmLocalized("event_added").ConfigureAwait(false);
        }

        public enum RpsPick
        {
            R = 0,
            Rock = 0,
            Rocket = 0,
            P = 1,
            Paper = 1,
            Paperclip = 1,
            S = 2,
            Scissors = 2
        }

        public enum RpsResult
        {
            Win,
            Loss,
            Draw,
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Rps(RpsPick pick, ShmartNumber amount = default)
        {
            long oldAmount = amount;
            if (!await CheckBetOptional(amount).ConfigureAwait(false) || (amount == 1))
                return;

            string getRpsPick(RpsPick p)
            {
                switch (p)
                {
                    case RpsPick.R:
                        return "🚀";
                    case RpsPick.P:
                        return "📎";
                    default:
                        return "✂️";
                }
            }
            var embed = new EmbedBuilder();

            var nadekoPick = (RpsPick)new NadekoRandom().Next(0, 3);

            if (amount > 0)
            {
                if (!await _cs.RemoveAsync(Context.User.Id,
                    "Rps-bet", amount, gamble: true).ConfigureAwait(false))
                {
                    await ReplyErrorLocalized("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                    return;
                }
            }

            string msg;
            if (pick == nadekoPick)
            {
                await _cs.AddAsync(Context.User.Id,
                    "Rps-draw", amount, gamble: true).ConfigureAwait(false);
                embed.WithOkColor();
                msg = GetText("rps_draw", getRpsPick(pick));
            }
            else if ((pick == RpsPick.Paper && nadekoPick == RpsPick.Rock) ||
                     (pick == RpsPick.Rock && nadekoPick == RpsPick.Scissors) ||
                     (pick == RpsPick.Scissors && nadekoPick == RpsPick.Paper))
            {
                amount = (long)(amount * Bc.BotConfig.BetflipMultiplier);
                await _cs.AddAsync(Context.User.Id,
                    "Rps-win", amount, gamble: true).ConfigureAwait(false);
                embed.WithOkColor();
                embed.AddField(GetText("won"), amount);
                msg = GetText("rps_win", Context.User.Mention,
                    getRpsPick(pick), getRpsPick(nadekoPick));
            }
            else
            {
                embed.WithErrorColor();
                amount = 0;
                msg = GetText("rps_win", Context.Client.CurrentUser.Mention, getRpsPick(nadekoPick),
                    getRpsPick(pick));
            }

            embed
                .WithDescription(msg);

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}

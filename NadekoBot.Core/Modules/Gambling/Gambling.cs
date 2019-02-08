﻿using Discord;
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
                if (_service.GetUserLevel(Context.User) < Bc.BotConfig.MinimumLevel)
                {
                    await ReplyErrorLocalized("lvl_rep", Bc.BotConfig.MinimumLevel).ConfigureAwait(false);
                    return;
                }

                TimeSpan? rem;
                if ((rem = _cache.AddRepGive(Context.User.Id, period)) != null)
                {
                    await ReplyErrorLocalized("rep_already_gived", rem?.ToString(@"dd\d\ hh\h\ mm\m\ ss\s")).ConfigureAwait(false);
                    return;
                }

                await _service.GiveReputation(target, Context.User);
                await ReplyConfirmLocalized("rep", Format.Bold(target.ToString()), period).ConfigureAwait(false);
            }
            else
                await ReplyErrorLocalized("self_rep").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        public async Task RepReset()
        {
            _cache.RemoveAllRepGives();
            await ReplyConfirmLocalized("timely_reset").ConfigureAwait(false);
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
                await ReplyErrorLocalized("timely_already_claimed", rem?.ToString(@"dd\d\ hh\h\ mm\m\ ss\s")).ConfigureAwait(false);
                return;
            }

            await _cs.AddAsync(Context.User.Id, "Timely claim", val).ConfigureAwait(false);

            await ReplyConfirmLocalized("timely", val + Bc.BotConfig.CurrencySign, period).ConfigureAwait(false);
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
        public async Task RaffleAward([Remainder] IRole role = null)
        {
            role = role ?? Context.Guild.EveryoneRole;

            var members = (await role.GetMembersAsync().ConfigureAwait(false));
            var membersArray = members as IUser[] ?? members.ToArray();
            if (membersArray.Length == 0)
            {
                return;
            }
            var usr = membersArray[new NadekoRandom().Next(0, membersArray.Length)];

            var user = usr as SocketGuildUser;
            var users = user as IGuildUser;
            var amount = 0;
            var awardRole1 = users.Guild.Roles.FirstOrDefault(x => x.Id == 461111950458224640);
            var awardRole2 = users.Guild.Roles.FirstOrDefault(x => x.Id == 405341253807374349);
            var awardRole3 = users.Guild.Roles.FirstOrDefault(x => x.Id == 405341261277560842);
            var awardRole4 = users.Guild.Roles.FirstOrDefault(x => x.Id == 475305705230958598);
            var awardRole5 = users.Guild.Roles.FirstOrDefault(x => x.Id == 425627158581346314);
            var awardRole6 = users.Guild.Roles.FirstOrDefault(x => x.Id == 475306009011683359);
            var awardRole7 = users.Guild.Roles.FirstOrDefault(x => x.Id == 405340766240636928);
            var awardRole8 = users.Guild.Roles.FirstOrDefault(x => x.Id == 475306363673772043);
            var awardRole9 = users.Guild.Roles.FirstOrDefault(x => x.Id == 405338590290378763);
            var awardRole10 = users.Guild.Roles.FirstOrDefault(x => x.Id == 425657999105982464);
            var awardRole11 = users.Guild.Roles.FirstOrDefault(x => x.Id == 408903151568158740);
            var awardRole12 = users.Guild.Roles.FirstOrDefault(x => x.Id == 408902786294480897);

            if (user.Roles.Contains(awardRole1))
                amount = 500;
            else
                if (user.Roles.Contains(awardRole2))
                amount = 1000;
            else
                if (user.Roles.Contains(awardRole3))
                amount = 1500;
            else
                if (user.Roles.Contains(awardRole4))
                amount = 2000;
            else
                if (user.Roles.Contains(awardRole5))
                amount = 3000;
            else
                if (user.Roles.Contains(awardRole6))
                amount = 4000;
            else
                if (user.Roles.Contains(awardRole7))
                amount = 5000;
            else
                if (user.Roles.Contains(awardRole8))
                amount = 7000;
            else
                if (user.Roles.Contains(awardRole9))
                amount = 10000;
            else
                if (user.Roles.Contains(awardRole10))
                amount = 15000;
            else
                if (user.Roles.Contains(awardRole11))
                amount = 30000;
            else
                if (user.Roles.Contains(awardRole12))
                amount = 50000;
            else
                amount = 500;

            await _cs.AddAsync(usr.Id,
                $"Awarded by raffle. ({Context.User.Username}/{Context.User.Id})",
                amount,
                gamble: (Context.Client.CurrentUser.Id != usr.Id)).ConfigureAwait(false);
            await Context.Channel.SendConfirmAsync("🎟 " + GetText("raffled_user", usr), $"{GetText("raffled_result", usr.Id, amount)}", footer: $"{usr.Id}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(1)]
        public async Task Cash([Remainder] IUser user = null)
        {
            user = user ?? Context.User;
            await ConfirmLocalized("has", Format.Bold(user.ToString()), $"{GetCurrency(user.Id)} {CurrencySign}").ConfigureAwait(false);
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
            }

            var success = await _cs.RemoveAsync((IGuildUser)Context.User, $"Gift to {receiver.Username} ({receiver.Id}).", amount, false).ConfigureAwait(false);
            if (!success)
            {
                await ReplyErrorLocalized("not_enough", CurrencyPluralName).ConfigureAwait(false);
                return;
            }
            await _cs.AddAsync(receiver, $"Gift from {Context.User.Username} ({Context.User.Id}) - {msg}.", amount, true).ConfigureAwait(false);
            await ReplyConfirmLocalized("gifted", amount + CurrencySign, Format.Bold(receiver.ToString()), msg)
                .ConfigureAwait(false);
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

                var success = await _cs.RemoveAsync((IGuildUser)Context.User, $"Invest into {club.Name} storage.", amount, false, gamble: true).ConfigureAwait(false);
                if (!success)
                {
                    await ReplyErrorLocalized("not_enough", CurrencyPluralName).ConfigureAwait(false);
                    return;
                }

                club.Currency += (int)amount;
                await uow.CompleteAsync();
                await ReplyConfirmLocalized("club_invested", amount + CurrencySign, Format.Bold(club.Name)).ConfigureAwait(false);
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
        public Task Award(ShmartNumber amount, IGuildUser usr, [Remainder] string msg) =>
            Award(amount, usr.Id, msg);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(1)]
        public Task Award(ShmartNumber amount, [Remainder] IGuildUser usr) =>
            Award(amount, usr.Id);

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        [Priority(2)]
        public async Task Award(ShmartNumber amount, ulong usrId, [Remainder] string msg = null)
        {
            if (amount <= 0)
                return;

            await _cs.AddAsync(usrId,
                $"Awarded by bot owner. ({Context.User.Username}/{Context.User.Id}) {(msg ?? "")}",
                amount,
                gamble: (Context.Client.CurrentUser.Id != usrId)).ConfigureAwait(false);
            await ReplyConfirmLocalized("awarded", amount + CurrencySign, $"<@{usrId}>").ConfigureAwait(false);
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
            var str = Format.Bold(Context.User.ToString()) + Format.Code(GetText("roll", rnd));
            if (rnd < 67)
            {
                str += GetText("better_luck");
            }
            else
            {
                long win;
                if (rnd < 91)
                {
                    win = (long)(amount * Bc.BotConfig.Betroll67Multiplier);
                    str += GetText("br_win", win + CurrencySign, 66);
                    await _cs.AddAsync(Context.User, "Betroll Gamble",
                        win, false, gamble: true).ConfigureAwait(false);
                }
                else if (rnd < 100)
                {
                    win = (long)(amount * Bc.BotConfig.Betroll91Multiplier);
                    str += GetText("br_win", win + CurrencySign, 90);
                    await _cs.AddAsync(Context.User, "Betroll Gamble",
                        win, false, gamble: true).ConfigureAwait(false);
                }
                else
                {
                    win = (long)(amount * Bc.BotConfig.Betroll100Multiplier);
                    str += GetText("br_win", win + CurrencySign, 99) + " 👑";
                    await _cs.AddAsync(Context.User, "Betroll Gamble",
                        win, false, gamble: true).ConfigureAwait(false);
                }
            }
            await Context.Channel.SendConfirmAsync(str).ConfigureAwait(false);
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
                                                 .WithValue(x.CurrencyAmount.ToString() + " " + CurrencySign)
                                                 .WithIsInline(true));
                    }
                    return embed;
                }
            }, 1000, 10, addPaginatedFooter: false);
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

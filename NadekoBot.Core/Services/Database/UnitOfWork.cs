﻿using NadekoBot.Core.Services.Database.Repositories;
using NadekoBot.Core.Services.Database.Repositories.Impl;
using System;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Database
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        public NadekoContext _context { get; }

        private IAchievementsRepository _achievements;
        public IAchievementsRepository Achievements => _achievements ?? (_achievements = new AchievementsRepository(_context));

        private IQuoteRepository _quotes;
        public IQuoteRepository Quotes => _quotes ?? (_quotes = new QuoteRepository(_context));

        private IGuildConfigRepository _guildConfigs;
        public IGuildConfigRepository GuildConfigs => _guildConfigs ?? (_guildConfigs = new GuildConfigRepository(_context));

        private IReminderRepository _reminders;
        public IReminderRepository Reminders => _reminders ?? (_reminders = new ReminderRepository(_context));

        private ISelfAssignedRolesRepository _selfAssignedRoles;
        public ISelfAssignedRolesRepository SelfAssignedRoles => _selfAssignedRoles ?? (_selfAssignedRoles = new SelfAssignedRolesRepository(_context));

        private IBotConfigRepository _botConfig;
        public IBotConfigRepository BotConfig => _botConfig ?? (_botConfig = new BotConfigRepository(_context));

        private ICurrencyTransactionsRepository _currencyTransactions;
        public ICurrencyTransactionsRepository CurrencyTransactions => _currencyTransactions ?? (_currencyTransactions = new CurrencyTransactionsRepository(_context));

        private IMusicPlaylistRepository _musicPlaylists;
        public IMusicPlaylistRepository MusicPlaylists => _musicPlaylists ?? (_musicPlaylists = new MusicPlaylistRepository(_context));

        private ICustomReactionRepository _customReactions;
        public ICustomReactionRepository CustomReactions => _customReactions ?? (_customReactions = new CustomReactionsRepository(_context));

        private IWaifuRepository _waifus;
        public IWaifuRepository Waifus => _waifus ?? (_waifus = new WaifuRepository(_context));

        private IDiscordUserRepository _discordUsers;
        public IDiscordUserRepository DiscordUsers => _discordUsers ?? (_discordUsers = new DiscordUserRepository(_context));

        private IWarningsRepository _warnings;
        public IWarningsRepository Warnings => _warnings ?? (_warnings = new WarningsRepository(_context));

        private IModLogRepository _modlog;
        public IModLogRepository ModLog => _modlog ?? (_modlog = new ModLogRepository(_context));

        private IRepLogRepository _replog;
        public IRepLogRepository RepLog => _replog ?? (_replog = new RepLogRepository(_context));

        private IRolesBonusRepository _rolesbonus;
        public IRolesBonusRepository RolesBonus => _rolesbonus ?? (_rolesbonus = new RolesBonusRepository(_context));

        private IEventScheduleRepository _eventschedule;
        public IEventScheduleRepository EventSchedule => _eventschedule ?? (_eventschedule = new EventScheduleRepository(_context));

        private IXpRepository _xp;
        public IXpRepository Xp => _xp ?? (_xp = new XpRepository(_context));

        private IClubRepository _clubs;
        public IClubRepository Clubs => _clubs ?? (_clubs = new ClubRepository(_context));

        private IPollsRepository _polls;
        public IPollsRepository Polls => _polls ?? (_polls = new PollsRepository(_context));

        private IPlantedCurrencyRepository _planted;
        public IPlantedCurrencyRepository PlantedCurrency => _planted ?? (_planted = new PlantedCurrencyRepository(_context));

        private IXpCardRepository _xpCards;
        public IXpCardRepository XpCards => _xpCards ?? (_xpCards = new XpCardRepository(_context));

        public UnitOfWork(NadekoContext context)
        {
            _context = context;
        }

        public int Complete() =>
            _context.SaveChanges();

        public Task<int> CompleteAsync() =>
            _context.SaveChangesAsync();

        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

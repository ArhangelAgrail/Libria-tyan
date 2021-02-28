using NadekoBot.Core.Services.Database.Repositories;
using System;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Database
{
    public interface IUnitOfWork : IDisposable
    {
        NadekoContext _context { get; }

        IAchievementsRepository Achievements { get; }
        IQuoteRepository Quotes { get; }
        IGuildConfigRepository GuildConfigs { get; }
        IReminderRepository Reminders { get; }
        ISelfAssignedRolesRepository SelfAssignedRoles { get; }
        IBotConfigRepository BotConfig { get; }
        ICustomReactionRepository CustomReactions { get; }
        ICurrencyTransactionsRepository CurrencyTransactions { get; }
        IMusicPlaylistRepository MusicPlaylists { get; }
        IWaifuRepository Waifus { get; }
        IDiscordUserRepository DiscordUsers { get; }
        IWarningsRepository Warnings { get; }
        IModLogRepository ModLog { get; }
        IRepLogRepository RepLog { get; }
        IEventScheduleRepository EventSchedule { get; }
        IXpRepository Xp { get; }
        IXpCardRepository XpCards { get; }
        IClubRepository Clubs { get; }
        IPollsRepository Polls { get; }
        IPlantedCurrencyRepository PlantedCurrency { get; }

        int Complete();
        Task<int> CompleteAsync();
    }
}

using NadekoBot.Core.Services.Database.Models;
using System;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IEventScheduleRepository : IRepository<EventSchedule>
    {
        EventSchedule[] ForId(ulong guildId, ulong userId);
        EventSchedule[] ByDate(ulong guildId, DateTime date);
    }
}

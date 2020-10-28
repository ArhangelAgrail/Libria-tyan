using System;
using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class EventScheduleRepository : Repository<EventSchedule>, IEventScheduleRepository
    {
        public EventScheduleRepository(DbContext context) : base(context)
        {
        }

        public EventSchedule[] ForId(ulong guildId, ulong userId)
        {
            var query = _set.Where(x => x.GuildId == guildId && x.UserId == userId)
                .OrderByDescending(x => x.DateAdded);

            return query.ToArray();
        }

        public EventSchedule[] ByDate(ulong guildId, DateTime date)
        {
            var query = _set.Where(x => x.GuildId == guildId && x.Date.DayOfYear == date.DayOfYear)
                .OrderBy(x => x.Date);

            return query.ToArray();
        }
    }
}

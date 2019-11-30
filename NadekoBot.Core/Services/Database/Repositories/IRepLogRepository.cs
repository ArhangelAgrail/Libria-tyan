using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IRepLogRepository : IRepository<RepLog>
    {
        IEnumerable<RepLogResult> GetRepLog(ulong userId, int count, int skip = 0);
        RepLogResult[] GetForUser(ulong userId);
        RepLogResult[] GetByUser(ulong userId);
    }
}

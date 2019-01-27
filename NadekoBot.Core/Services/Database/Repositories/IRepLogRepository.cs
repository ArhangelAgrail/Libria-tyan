using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IRepLogRepository : IRepository<RepLog>
    {
        IEnumerable<RepLogResult> GetRepLog(ulong userId, int count, int skip = 0);
    }
}

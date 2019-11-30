using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Services.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class RepLogRepository : Repository<RepLog>, IRepLogRepository
    {
        public RepLogRepository(DbContext context) : base(context)
        {
        }

        public IEnumerable<RepLogResult> GetRepLog(ulong userId, int count, int skip = 0)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return new List<RepLogResult>();

            return _set
                .Skip(skip)
                .Select(x => new RepLogResult
                {
                    UserId = x.FromId,
                })
                .Where(x => x.UserId == userId)
                .Take(count)
                .ToList();
        }

        public RepLogResult[] GetForUser(ulong userId)
        {
            return _set
                .Where(x => x.UserId == userId)
                .GroupBy(x => x.FromId)
                .Select(x => new RepLogResult
                {
                    UserId = x.Key,
                    Count = x.Count()
                }).ToArray();

        }

        public RepLogResult[] GetByUser(ulong userId)
        {
            return _set
                .Where(x => x.FromId == userId)
                .GroupBy(x => x.UserId)
                .Select(x => new RepLogResult
                {
                    UserId = x.Key,
                    Count = x.Count()
                }).ToArray();

        }
    }
}

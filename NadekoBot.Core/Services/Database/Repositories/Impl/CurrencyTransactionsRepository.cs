using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class CurrencyTransactionsRepository : Repository<CurrencyTransaction>, ICurrencyTransactionsRepository
    {
        public CurrencyTransactionsRepository(DbContext context) : base(context)
        {
        }

        public List<CurrencyTransaction> GetPageFor(ulong userId, int page)
        {
            return _set.Where(x => x.UserId == userId)
                .OrderByDescending(x => x.DateAdded)
                .Skip(15 * page)
                .Take(15)
                .ToList();
        }

        public string GetInvestedAmount(ulong userId, string clubName)
        {
            string reason = $"Invest into {clubName} storage.";
            return _set.Where(x => x.UserId == userId && x.Reason == reason)
                .Sum(x => x.Amount)
                .ToString();
        }

        public int GetClubAwarded(ulong userId)
        {
            string reason = "Club Award";
            var AllAwards = _set.Where(x => x.UserId == userId && x.Reason == reason)
                .ToList();

            if (AllAwards.Count == 0)
                return 0;

            int inUse = 0, used = 0;
            var last = AllAwards.Last();
            var previous = AllAwards.First().DateAdded;
            foreach (var award in AllAwards)
            {
                used = (int)_set.Where(x => x.UserId == userId && x.DateAdded > previous && x.DateAdded < award.DateAdded)
                    .Where(x => x.Reason == "Shop purchase - Role" || x.Reason == "Claimed Waifu" || x.Reason == "Bought waifu item" || x.Reason == "Immune set" || x.Reason == "Waifu Reset")
                    .Sum(x => x.Amount);

                inUse += (int)award.Amount + used;

                if (inUse <= 0)
                    inUse = 0;

                previous = award.DateAdded;
            }

            used = (int)_set.Where(x => x.UserId == userId && x.DateAdded > last.DateAdded)
                    .Where(x => x.Reason == "Shop purchase - Role" || x.Reason == "Claimed Waifu" || x.Reason == "Bought waifu item" || x.Reason == "Immune set" || x.Reason == "Waifu Reset")
                    .Sum(x => x.Amount);

            inUse += (int)last.Amount + used;

            return inUse;
        }
    }
}

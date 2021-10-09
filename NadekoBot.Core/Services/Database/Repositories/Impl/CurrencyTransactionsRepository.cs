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
            var Date = _set.Where(x => x.Reason.Contains("Invest into") || x.Reason.Contains("Gift to")).Last().DateAdded;
            var AllAwards = _set.Where(x => x.UserId == userId && x.Reason == reason && x.DateAdded > Date)
                .ToList();

            if (AllAwards.Count == 0)
                return 0;

            int inUse = (int)AllAwards.First().Amount, used = 0;
            var last = AllAwards.Last();
            var previous = AllAwards.First().DateAdded;
            foreach (var award in AllAwards.Skip(1))
            {
                used = (int)_set.Where(x => x.UserId == userId && x.DateAdded > previous && x.DateAdded < award.DateAdded)
                    .Where(x => x.Reason == "Shop purchase - Role" || x.Reason == "Claimed Waifu" || x.Reason == "Bought waifu item" || x.Reason == "Immune set" || x.Reason == "Waifu Reset")
                    .Sum(x => x.Amount);

                inUse += used;

                if (inUse <= 0)
                    inUse = 0;

                inUse += (int)award.Amount;

                previous = award.DateAdded;
            }

            used = (int)_set.Where(x => x.UserId == userId && x.DateAdded > last.DateAdded)
                    .Where(x => x.Reason == "Shop purchase - Role" || x.Reason == "Claimed Waifu" || x.Reason == "Bought waifu item" || x.Reason == "Immune set" || x.Reason == "Waifu Reset")
                    .Sum(x => x.Amount);

            inUse += used;

            return inUse;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Services.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IClubRepository : IRepository<ClubInfo>
    {
        int GetNextDiscrim(string clubName);
        new IEnumerable<ClubInfo> GetAll();
        ClubInfo GetByName(string v/*, int discrim*/, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null);
        ClubInfo GetByOwner(ulong userId, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null);
        ClubInfo GetByOwnerOrAdmin(ulong userId);
        ClubInfo GetByMember(ulong userId, Func<DbSet<ClubInfo>, IQueryable<ClubInfo>> func = null);
        ClubInfo[] GetClubLeaderboardPage(int page);
    }
}

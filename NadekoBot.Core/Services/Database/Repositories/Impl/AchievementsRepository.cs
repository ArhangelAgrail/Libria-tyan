using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class AchievementsRepository : Repository<Achievements>, IAchievementsRepository
    {
        public AchievementsRepository(DbContext context) : base(context)
        {
        }

        public IEnumerable<Achievements> ByGroup(string GroupName) 
            => _set.Where(x => x.GroupName == GroupName)
            .ToArray();

        public Achievements[] ByRoleId(ulong RoleId)
            => _set.Where(x => x.RoleId == RoleId)
            .ToArray();

        public Achievements[] GetAllAchievements()
            => _set.ToArray();
    }
}

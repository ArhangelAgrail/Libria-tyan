using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IAchievementsRepository : IRepository<Achievements>
    {
        IEnumerable<Achievements> ByGroup(string GroupName);
        Achievements[] ByRoleId(ulong RoleId);
        Achievements[] GetAllAchievements();
    }
}

using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IRolesBonusRepository : IRepository<RolesBonus>
    {
        RolesBonus[] ByRoleId(ulong RoleId);
        RolesBonus[] GetAllBonus();
    }
}

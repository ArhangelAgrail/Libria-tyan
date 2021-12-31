using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class RolesBonusRepository : Repository<RolesBonus>, IRolesBonusRepository
    {
        public RolesBonusRepository(DbContext context) : base(context)
        {
        }

        public RolesBonus[] ByRoleId(ulong RoleId)
            => _set.Where(x => x.RoleId == RoleId)
            .ToArray();

        public RolesBonus[] GetAllBonus()
            => _set.ToArray();
    }
}

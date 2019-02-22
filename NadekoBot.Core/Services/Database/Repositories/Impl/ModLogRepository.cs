using System;
using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class ModLogRepository : Repository<ModLog>, IModLogRepository
    {
        public ModLogRepository(DbContext context) : base(context)
        {
        }

        public ModLog[] ForId(ulong guildId, ulong userId)
        {
            var query = _set.Where(x => x.GuildId == guildId && x.UserId == userId)
                .OrderByDescending(x => x.DateAdded);

            return query.ToArray();
        }

        public ModLog[] ByModerator(ulong guildId, ulong moderator)
        {
            var query = _set.Where(x => x.GuildId == guildId && x.Moderator == moderator);

            return query.ToArray();
        }

        
        public ModLog[] ByGuild(ulong guildId)
        {
            var query = _set.Where(x => x.GuildId == guildId);

            return query.ToArray();
        }
    }
}

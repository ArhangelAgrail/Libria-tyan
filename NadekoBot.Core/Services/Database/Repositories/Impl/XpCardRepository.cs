using NadekoBot.Core.Services.Database.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class XpCardRepository : Repository<XpCard>, IXpCardRepository
    {
        public XpCardRepository(DbContext context) : base(context)
        {
        }

        public void AddXpCard(string name, ulong roleId, int image)
        {
            _context.Database.ExecuteSqlCommand($"INSERT INTO XpCards (Name, RoleId, Image) VALUES ({name}, {roleId}, {image});");
        }

        public void DelXpCard(string name)
        {
            _context.Database.ExecuteSqlCommand($"DELETE FROM XpCards WHERE Name={name};");
        }

        public void SetXpCard(ulong userId, string name)
        {
            _context.Database.ExecuteSqlCommand($"UPDATE DiscordUser SET XpCardImage=(SELECT Image FROM XpCards WHERE Name={name}) WHERE UserId={userId};");
            _context.Database.ExecuteSqlCommand($"UPDATE DiscordUser SET XpCardRole=(SELECT RoleId FROM XpCards WHERE Name={name}) WHERE UserId={userId};");
        }

        public void SetDefault(ulong userId)
        {
            _context.Database.ExecuteSqlCommand($"UPDATE DiscordUser SET XpCardImage=0 WHERE UserId={userId};");
            _context.Database.ExecuteSqlCommand($"UPDATE DiscordUser SET XpCardRole=0 WHERE UserId={userId};");
        }

        public void SetClubCard(ulong userId, ulong roleId)
        {
            _context.Database.ExecuteSqlCommand($"UPDATE DiscordUser SET XpCardImage=111 WHERE UserId={userId};");
            _context.Database.ExecuteSqlCommand($"UPDATE DiscordUser SET XpCardRole={roleId} WHERE UserId={userId};");
        }

        public ulong GetXpCardRoleId(string name)
        {
            return _set.Where(x => x.Name == name).Select(y => y.RoleId).FirstOrDefault();
        }

        public XpCardResult[] GetAllXpCards()
        {
            return _set.Select(x => new XpCardResult
            {
                Name = x.Name
            }).ToArray();
        }
    }
}

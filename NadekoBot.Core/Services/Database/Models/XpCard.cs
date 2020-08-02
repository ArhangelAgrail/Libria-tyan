using System;

namespace NadekoBot.Core.Services.Database.Models
{
    public class XpCard : DbEntity
    {
        public string Name { get; set; }
        public ulong RoleId { get; set; }
        public int Image { get; set; }
    }

    public class XpCardResult
    {
        public string Name { get; set; }
    }
}
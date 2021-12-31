namespace NadekoBot.Core.Services.Database.Models
{
    public class RolesBonus : DbEntity
    {
        public ulong RoleId { get; set; }
        public int Bonus { get; set; }
    } 
}

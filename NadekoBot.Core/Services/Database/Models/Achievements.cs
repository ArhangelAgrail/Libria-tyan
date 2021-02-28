namespace NadekoBot.Core.Services.Database.Models
{
    public class Achievements : DbEntity
    {
        public ulong RoleId { get; set; }
        public string GroupName { get; set; }
        public int Condition { get; set; }
    } 
}

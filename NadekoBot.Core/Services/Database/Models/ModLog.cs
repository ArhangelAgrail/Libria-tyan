namespace NadekoBot.Core.Services.Database.Models
{
    public class ModLog : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public string Reason { get; set; }
        public string Type { get; set; }
        public ulong Moderator { get; set; }
    }
}

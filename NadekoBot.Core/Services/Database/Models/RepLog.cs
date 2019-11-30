namespace NadekoBot.Core.Services.Database.Models
{
    public class RepLog : DbEntity
    {
        public ulong UserId { get; set; }
        public ulong FromId { get; set; }
    }

    public class RepLogResult
    {
        public ulong UserId { get; set; }
        public int Count { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Dynamic;

namespace NadekoBot.Core.Services.Database.Models
{
    public class EventSchedule: DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }
}

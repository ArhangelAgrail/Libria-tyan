using NadekoBot.Extensions;
using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Models
{
    public class WaifuInfo : DbEntity
    {
        public int WaifuId { get; set; }
        public DiscordUser Waifu { get; set; }

        public int? ClaimerId { get; set; }
        public DiscordUser Claimer { get; set; }

        public int? AffinityId { get; set; }
        public DiscordUser Affinity { get; set; }

        public bool Immune { get; set; }
        public int Reputation { get; set; }
        public ulong LastReputation { get; set; }
        public string Info { get; set; }

        public int Price { get; set; }
        public List<WaifuItem> Items { get; set; } = new List<WaifuItem>();

        public override string ToString()
        {
            var claimer = "никем";
            var status = "";

            var waifuUsername = Waifu.Username.TrimTo(20);
            var claimerUsername = Claimer?.Username.TrimTo(20);

            if (ClaimerId != null)
            {
                claimer = $"{ claimerUsername }#{Claimer.Discriminator}";
            }
            if (AffinityId == null)
            {
                status = $"... но сердце {waifuUsername} никому не принадлежит";
            }
            else if (AffinityId == ClaimerId)
            {
                status = $"... и {waifuUsername} тоже в восторге от {claimerUsername} >:з";
            }
            else
            {
                status = $"... но сердце {waifuUsername} принадлежит {Affinity.Username.TrimTo(20)}#{Affinity.Discriminator}";
            }

            if (ClaimerId == null)
            {
                return $"**{waifuUsername}#{Waifu.Discriminator}** - {claimer} не присвоен\n\t{status}";
            }
            else
                return $"**{waifuUsername}#{Waifu.Discriminator}** - присвоен **{claimer}**\n\t{status}";

        }
    }

    public class RepLbResult
    {
        public string Username { get; set; }
        public string Discrim { get; set; }

        public int Reputation { get; set; }
    }

    public class WaifuLbResult
    {
        public string Username { get; set; }
        public string Discrim { get; set; }

        public string Claimer { get; set; }
        public string ClaimerDiscrim { get; set; }

        public string Affinity { get; set; }
        public string AffinityDiscrim { get; set; }

        public int Price { get; set; }

        public override string ToString()
        {
            var claimer = "никем";
            var status = "";

            var waifuUsername = Username.TrimTo(20);
            var claimerUsername = Claimer?.TrimTo(20);

            if (Claimer != null)
            {
                claimer = $"{ claimerUsername }#{ClaimerDiscrim}";
            }
            if (Affinity == null)
            {
                status = $"... но сердце {waifuUsername} никому не принадлежит";
            }
            else if (Affinity + AffinityDiscrim == Claimer + ClaimerDiscrim)
            {
                status = $"... и {waifuUsername} тоже в восторге от {claimerUsername} >:з";
            }
            else
            {
                status = $"... но сердце {waifuUsername} принадлежит {Affinity.TrimTo(20)}#{AffinityDiscrim}";
            }

            if (Claimer == null)
            {
                return $"**{waifuUsername}#{Discrim}** - {claimer} не присвоен\n\t{status}";
            }
            else
                return $"**{waifuUsername}#{Discrim}** - присвоен **{claimer}**\n\t{status}";
        }
    }
}
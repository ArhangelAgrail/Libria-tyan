using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IXpCardRepository : IRepository<XpCard>
    {
        void AddXpCard(string name, ulong roleId, int image);
        void DelXpCard(string name);
        void SetDefault(ulong userId);
        void SetClubCard(ulong userId, ulong roleId);
        void SetXpCard(ulong userId, string name);
        ulong GetXpCardRoleId(string name);
        XpCardResult[] GetAllXpCards();
    }
}

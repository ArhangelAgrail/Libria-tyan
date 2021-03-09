using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;

namespace NadekoBot.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class InfoCommands : NadekoSubmodule
        {
            private readonly DbService _db;
            private readonly DiscordSocketClient _client;
            private readonly IStatsService _stats;

            public InfoCommands(DbService db, DiscordSocketClient client, IStatsService stats)
            {
                _db = db;
                _client = client;
                _stats = stats;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ServerInfo(string guildName = null)
            {
                var channel = (ITextChannel)Context.Channel;
                guildName = guildName?.ToUpperInvariant();
                SocketGuild guild;
                if (string.IsNullOrWhiteSpace(guildName))
                    guild = (SocketGuild)channel.Guild;
                else
                    guild = _client.Guilds.FirstOrDefault(g => g.Name.ToUpperInvariant() == guildName.ToUpperInvariant());
                if (guild == null)
                    return;
                var ownername = guild.GetUser(guild.OwnerId);
                var textchn = guild.TextChannels.Count();
                var voicechn = guild.VoiceChannels.Count();

                var createdAt = new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(guild.Id >> 22);
                var features = string.Join("\n", guild.Features);
                if (string.IsNullOrWhiteSpace(features))
                    features = "-";
                var embed = new EmbedBuilder()
                    .WithAuthor(eab => eab.WithName(GetText("server_info")))
                    .WithTitle(guild.Name)
                    .AddField(fb => fb.WithName(GetText("id")).WithValue(guild.Id.ToString()).WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("owner")).WithValue(ownername.ToString()).WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("members")).WithValue(guild.MemberCount.ToString()).WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("text_channels")).WithValue(textchn.ToString()).WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("voice_channels")).WithValue(voicechn.ToString()).WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("created_at")).WithValue($"{createdAt:dd.MM.yyyy HH:mm}").WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("region")).WithValue(guild.VoiceRegionId.ToString()).WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("roles")).WithValue((guild.Roles.Count - 1).ToString()).WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("features")).WithValue(features).WithIsInline(true))
                    .WithColor(NadekoBot.OkColor);
                if (Uri.IsWellFormedUriString(guild.IconUrl, UriKind.Absolute))
                    embed.WithThumbnailUrl(guild.IconUrl);
                if (guild.Emotes.Any())
                {
                    embed.AddField(fb => 
                        fb.WithName(GetText("custom_emojis") + $"({guild.Emotes.Count})")
                        .WithValue(string.Join(" ", guild.Emotes
                            .Shuffle()
                            .Take(20)
                            .Select(e => $"{e.Name} {e.ToString()}"))
                            .TrimTo(1020)));
                }
                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChannelInfo(ITextChannel channel = null)
            {
                var ch = channel ?? (ITextChannel)Context.Channel;
                if (ch == null)
                    return;
                var createdAt = new DateTime(2015, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ch.Id >> 22);
                var usercount = (await ch.GetUsersAsync().FlattenAsync().ConfigureAwait(false)).Count();
                var embed = new EmbedBuilder()
                    .WithTitle(ch.Name)
                    .WithDescription(ch.Topic?.SanitizeMentions())
                    .AddField(fb => fb.WithName(GetText("id")).WithValue(ch.Id.ToString()).WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("created_at")).WithValue($"{createdAt:dd.MM.yyyy HH:mm}").WithIsInline(true))
                    .AddField(fb => fb.WithName(GetText("users")).WithValue(usercount.ToString()).WithIsInline(true))
                    .WithColor(NadekoBot.OkColor);
                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task UserInfo(IGuildUser usr = null)
            {
                var user = usr ?? Context.User as IGuildUser;
                var time = DateTime.UtcNow - user.JoinedAt;

                using (var uow = _db.UnitOfWork)
                {
                    var du = uow.DiscordUsers.GetOrCreate(user);
                    var firstTime = DateTime.UtcNow - du.DateAdded;

                    await uow.CompleteAsync();

                    if (user == null)
                        return;

                    var embed = new EmbedBuilder();
                    if (!string.IsNullOrWhiteSpace(user.Nickname))
                        embed.WithAuthor(user.Nickname);

                    embed.WithTitle($"{user.Username}#{user.Discriminator}")
                        .AddField(fb => fb.WithName(GetText("joined_server")).WithValue($"{user.JoinedAt?.ToString("dd.MM.yyyy HH:mm") ?? "?"} ({time:dd} {GetText("days")})").WithIsInline(true))
                        .AddField(fb => fb.WithName(GetText("first_server_join")).WithValue($"{du.DateAdded?.ToString("dd.MM.yyyy HH:mm") ?? "?"} ({firstTime:dd} {GetText("days")})").WithIsInline(true))
                        .AddField(fb => fb.WithName(GetText("joined_discord")).WithValue($"{user.CreatedAt:dd.MM.yyyy HH:mm}").WithIsInline(true))
                        .AddField(fb => fb.WithName($"{GetText("roles")} ({ user.RoleIds.Count - 1})").WithValue($"{string.Join(" | ", user.GetRoles().Skip(1).Select(r => { return $"<@&{r.Id}>"; })).SanitizeMentions()}").WithIsInline(false))
                        .WithFooter(user.Id.ToString(), "https://cdn.discordapp.com/attachments/404549045168766986/649672628298055695/icon-45.png")
                        .WithColor(NadekoBot.OkColor);

                    var av = user.RealAvatarUrl();
                    if (av != null && av.IsAbsoluteUri)
                    {
                        embed.WithUrl(av.ToString())
                            .WithThumbnailUrl(av.ToString());
                    }

                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task Activity(int page = 1)
            {
                const int activityPerPage = 10;
                page -= 1;

                if (page < 0)
                    return;

                int startCount = page * activityPerPage;

                StringBuilder str = new StringBuilder();
                foreach (var kvp in CmdHandler.UserMessagesSent.OrderByDescending(kvp => kvp.Value).Skip(page * activityPerPage).Take(activityPerPage))
                {
                    str.AppendLine(GetText("activity_line",
                        ++startCount,
                        $"<@{kvp.Key.ToString()}>",
                        kvp.Value / _stats.GetUptime().TotalMinutes, kvp.Value));
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder()
                    .WithTitle(GetText("activity_page", page + 1))
                    .WithOkColor()
                    .WithFooter(efb => efb.WithText(GetText("activity_users_total",
                        CmdHandler.UserMessagesSent.Count)))
                    .WithDescription(str.ToString())).ConfigureAwait(false);
            }
        }
    }
}

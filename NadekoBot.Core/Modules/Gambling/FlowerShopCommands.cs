﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.Collections;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Extensions;

namespace NadekoBot.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class FlowerShopCommands : NadekoSubmodule
        {
            private readonly DbService _db;
            private readonly ICurrencyService _cs;
            private readonly DiscordSocketClient _client;

            public enum Role
            {
                Role
            }

            public enum List
            {
                List
            }

            public FlowerShopCommands(DbService db, ICurrencyService cs, DiscordSocketClient client)
            {
                _db = db;
                _cs = cs;
                _client = client;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Shop(int page = 1)
            {
                if (--page < 0)
                    return;
                List<ShopEntry> entries;
                int reputation, price;

                using (var uow = _db.UnitOfWork)
                {
                    entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(Context.Guild.Id,
                        set => set.Include(x => x.ShopEntries)
                                  .ThenInclude(x => x.Items)).ShopEntries);

                    reputation = uow.Waifus.GetWaifuInfo(Context.User.Id).Reputation;
                    price = uow.Waifus.GetWaifuInfo(Context.User.Id).Price;
                }

                await Context.SendPaginatedConfirmAsync(page, (curPage) =>
                {
                    var theseEntries = entries.Skip(curPage * 9).Take(9).ToArray();

                    if (reputation > 2500) reputation = 2500;
                    if (price > 500000) price = 500000;

                    if (!theseEntries.Any())
                        return new EmbedBuilder().WithErrorColor()
                            .WithDescription(GetText("shop_none"));

                    var discount = reputation * 0.01 + price * 0.00005;

                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle(GetText("shop", Bc.BotConfig.CurrencySign) + " - " + GetText("shop_discount", String.Format("{0:0.##}", discount)))
                        .WithDescription(GetText("shop_discount_desc", reputation, price));

                    for (int i = 0; i < theseEntries.Length; i++)
                    {
                        var entry = theseEntries[i];
                        if (curPage * 9 + i + 1 == 1)
                            embed.AddField(efb => efb.WithName($"#{curPage * 9 + i + 1} - {GetText("shop_unique")}\n{GetText("shop_price", entry.Price, Bc.BotConfig.CurrencySign)}").WithValue($"<@&{entry.RoleId}>").WithIsInline(true));
                        else
                            embed.AddField(efb => efb.WithName($"#{curPage * 9 + i + 1} - ~~{entry.Price}~~{Bc.BotConfig.CurrencySign}\n{GetText("shop_price", (int)(entry.Price - entry.Price * (discount * 0.01)), Bc.BotConfig.CurrencySign)}").WithValue(GetText("shop_role", $"<@&{entry.RoleId}>")).WithIsInline(true));
                    }
                    return embed;
                }, entries.Count, 9, true).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Buy(int index)
            {
                index -= 1;
                if (index < 0)
                    return;
                ShopEntry entry;
                int reputation, price;

                using (var uow = _db.UnitOfWork)
                {
                    var config = uow.GuildConfigs.ForId(Context.Guild.Id, set => set
                        .Include(x => x.ShopEntries)
                        .ThenInclude(x => x.Items));
                    var entries = new IndexedCollection<ShopEntry>(config.ShopEntries);
                    entry = entries.ElementAtOrDefault(index);

                    reputation = uow.Waifus.GetWaifuInfo(Context.User.Id).Reputation;
                    price = uow.Waifus.GetWaifuInfo(Context.User.Id).Price;

                    if (index == 0)
                    {
                        var guser = (IGuildUser)Context.User;
                        var role = Context.Guild.GetRole(entry.RoleId);

                        if (role == null)
                        {
                            await ReplyErrorLocalized("shop_role_not_found").ConfigureAwait(false);
                            uow.Complete();
                            return;
                        }

                        if (await _cs.RemoveAsync(Context.User.Id, $"Shop purchase - {entry.Type}", entry.Price).ConfigureAwait(false))
                        {
                            var previousUser = Context.Guild.GetUserAsync(entry.AuthorId).Result;
                            if (previousUser != null)
                                try
                                {
                                    await previousUser.RemoveRoleAsync(role).ConfigureAwait(false);
                                }
                                catch { }
                            try
                            {
                                await guser.AddRoleAsync(role).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _log.Warn(ex);
                                await previousUser.AddRoleAsync(role).ConfigureAwait(false);
                                await _cs.AddAsync(Context.User.Id, $"Shop error refund", entry.Price).ConfigureAwait(false);
                                await ReplyErrorLocalized("shop_role_purchase_error").ConfigureAwait(false);
                                uow.Complete();
                                return;
                            }

                            entry.Price += 10000;
                            entry.AuthorId = guser.Id;

                            await ConfirmLocalized("shop_role_unique_purchase", $"<@&{role.Id}>", Format.Bold(entry.Price.ToString()), Bc.BotConfig.CurrencySign, $"<@{entry.AuthorId}>").ConfigureAwait(false);
                            uow.Complete();
                            return;
                        }
                        else
                        {
                            await ReplyErrorLocalized("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                            uow.Complete();
                            return;
                        }
                    }

                    uow.Complete();
                }

                if (reputation > 2500) reputation = 2500;
                if (price > 500000) price = 500000;

                if (entry == null)
                {
                    await ReplyErrorLocalized("shop_item_not_found").ConfigureAwait(false);
                    return;
                }

                var discount = reputation * 0.01 + price * 0.00005;

                if (entry.Type == ShopEntryType.Role)
                {
                    var guser = (IGuildUser)Context.User;
                    var role = Context.Guild.GetRole(entry.RoleId);

                    if (role == null)
                    {
                        await ReplyErrorLocalized("shop_role_not_found").ConfigureAwait(false);
                        return;
                    }

                    if (await _cs.RemoveAsync(Context.User.Id, $"Shop purchase - {entry.Type}", (int)(entry.Price - entry.Price * (discount * 0.01))).ConfigureAwait(false))
                    {
                        try
                        {
                            await guser.AddRoleAsync(role).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _log.Warn(ex);
                            await _cs.AddAsync(Context.User.Id, $"Shop error refund", (int)(entry.Price - entry.Price * (discount * 0.01))).ConfigureAwait(false);
                            await ReplyErrorLocalized("shop_role_purchase_error").ConfigureAwait(false);
                            return;
                        }
                        //var profit = GetProfitAmount(entry.Price);
                        //await _cs.AddAsync(entry.AuthorId, $"Shop sell item - {entry.Type}", profit).ConfigureAwait(false);
                        //await _cs.AddAsync(Context.Client.CurrentUser.Id, $"Shop sell item - cut", entry.Price - profit).ConfigureAwait(false);
                        await ReplyConfirmLocalized("shop_role_purchase", $"<@&{role.Id}>").ConfigureAwait(false);
                        return;
                    }
                    else
                    {
                        await ReplyErrorLocalized("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                        return;
                    }
                }
                else if (entry.Type == ShopEntryType.List)
                {
                    if (entry.Items.Count == 0)
                    {
                        await ReplyErrorLocalized("out_of_stock").ConfigureAwait(false);
                        return;
                    }

                    var item = entry.Items.ToArray()[new NadekoRandom().Next(0, entry.Items.Count)];

                    if (await _cs.RemoveAsync(Context.User.Id, $"Shop purchase - {entry.Type}", (int)(entry.Price - entry.Price * (reputation * 0.0001 + price * 0.0000005))).ConfigureAwait(false))
                    {
                        using (var uow = _db.UnitOfWork)
                        {
                            var x = uow._context.Set<ShopEntryItem>().Remove(item);
                            uow.Complete();
                        }
                        try
                        {
                            await (await Context.User.GetOrCreateDMChannelAsync().ConfigureAwait(false))
                                .EmbedAsync(new EmbedBuilder().WithOkColor()
                                .WithTitle(GetText("shop_purchase", Context.Guild.Name))
                                .AddField(efb => efb.WithName(GetText("item")).WithValue(item.Text).WithIsInline(false))
                                .AddField(efb => efb.WithName(GetText("price")).WithValue(((int)(entry.Price - entry.Price * (reputation * 0.0001 + price * 0.0000005))).ToString()).WithIsInline(true))
                                .AddField(efb => efb.WithName(GetText("name")).WithValue(entry.Name).WithIsInline(true)))
                                .ConfigureAwait(false);

                            //await _cs.AddAsync(entry.AuthorId,
                            //$"Shop sell item - {entry.Name}",
                            //GetProfitAmount((int)(entry.Price - entry.Price * (reputation * 0.0002 + price * 0.0000005)))).ConfigureAwait(false);
                        }
                        catch
                        {
                            await _cs.AddAsync(Context.User.Id,
                                $"Shop error refund - {entry.Name}",
                                (int)(entry.Price - entry.Price * (reputation * 0.0001 + price * 0.0000005))).ConfigureAwait(false);
                            using (var uow = _db.UnitOfWork)
                            {
                                var entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(Context.Guild.Id,
                                    set => set.Include(x => x.ShopEntries)
                                              .ThenInclude(x => x.Items)).ShopEntries);
                                entry = entries.ElementAtOrDefault(index);
                                if (entry != null)
                                {
                                    if (entry.Items.Add(item))
                                    {
                                        uow.Complete();
                                    }
                                }
                            }
                            await ReplyErrorLocalized("shop_buy_error").ConfigureAwait(false);
                            return;
                        }
                        await ReplyConfirmLocalized("shop_item_purchase").ConfigureAwait(false);
                    }
                    else
                    {
                        await ReplyErrorLocalized("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                        return;
                    }
                }

            }

            private static long GetProfitAmount(int price) =>
                (int)(Math.Ceiling(0.90 * price));

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireBotPermission(GuildPermission.ManageRoles)]
            public async Task ShopAdd(Role _, int price, [Remainder] IRole role)
            {
                var entry = new ShopEntry()
                {
                    Name = "-",
                    Price = price,
                    Type = ShopEntryType.Role,
                    AuthorId = Context.User.Id,
                    RoleId = role.Id,
                    RoleName = role.Name
                };
                using (var uow = _db.UnitOfWork)
                {
                    var entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(Context.Guild.Id,
                        set => set.Include(x => x.ShopEntries)
                                  .ThenInclude(x => x.Items)).ShopEntries)
                    {
                        entry
                    };
                    uow.GuildConfigs.ForId(Context.Guild.Id, set => set).ShopEntries = entries;
                    uow.Complete();
                }
                await Context.Channel.EmbedAsync(EntryToEmbed(entry)
                    .WithTitle(GetText("shop_item_add"))).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task ShopAdd(List _, int price, [Remainder]string name)
            {
                var entry = new ShopEntry()
                {
                    Name = name.TrimTo(100),
                    Price = price,
                    Type = ShopEntryType.List,
                    AuthorId = Context.User.Id,
                    Items = new HashSet<ShopEntryItem>(),
                };
                using (var uow = _db.UnitOfWork)
                {
                    var entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(Context.Guild.Id,
                        set => set.Include(x => x.ShopEntries)
                                  .ThenInclude(x => x.Items)).ShopEntries)
                    {
                        entry
                    };
                    uow.GuildConfigs.ForId(Context.Guild.Id, set => set).ShopEntries = entries;
                    uow.Complete();
                }
                await Context.Channel.EmbedAsync(EntryToEmbed(entry)
                    .WithTitle(GetText("shop_item_add"))).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task ShopListAdd(int index, [Remainder] string itemText)
            {
                index -= 1;
                if (index < 0)
                    return;
                var item = new ShopEntryItem()
                {
                    Text = itemText
                };
                ShopEntry entry;
                bool rightType = false;
                bool added = false;
                using (var uow = _db.UnitOfWork)
                {
                    var entries = new IndexedCollection<ShopEntry>(uow.GuildConfigs.ForId(Context.Guild.Id,
                        set => set.Include(x => x.ShopEntries)
                                  .ThenInclude(x => x.Items)).ShopEntries);
                    entry = entries.ElementAtOrDefault(index);
                    if (entry != null && (rightType = (entry.Type == ShopEntryType.List)))
                    {
                        if (added = entry.Items.Add(item))
                        {
                            uow.Complete();
                        }
                    }
                }
                if (entry == null)
                    await ReplyErrorLocalized("shop_item_not_found").ConfigureAwait(false);
                else if (!rightType)
                    await ReplyErrorLocalized("shop_item_wrong_type").ConfigureAwait(false);
                else if (added == false)
                    await ReplyErrorLocalized("shop_list_item_not_unique").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalized("shop_list_item_added").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task ShopRemove(int index)
            {
                index -= 1;
                if (index < 0)
                    return;
                ShopEntry removed;
                using (var uow = _db.UnitOfWork)
                {
                    var config = uow.GuildConfigs.ForId(Context.Guild.Id, set => set
                        .Include(x => x.ShopEntries)
                        .ThenInclude(x => x.Items));

                    var entries = new IndexedCollection<ShopEntry>(config.ShopEntries);
                    removed = entries.ElementAtOrDefault(index);
                    if (removed != null)
                    {
                        uow._context.RemoveRange(removed.Items);
                        uow._context.Remove(removed);
                        uow.Complete();
                    }
                }

                if (removed == null)
                    await ReplyErrorLocalized("shop_item_not_found").ConfigureAwait(false);
                else
                    await Context.Channel.EmbedAsync(EntryToEmbed(removed)
                        .WithTitle(GetText("shop_item_rm"))).ConfigureAwait(false);
            }

            public EmbedBuilder EntryToEmbed(ShopEntry entry)
            {
                var embed = new EmbedBuilder().WithOkColor();

                if (entry.Type == ShopEntryType.Role)
                    return embed.AddField(efb => efb.WithName(GetText("name")).WithValue(GetText("shop_role", Format.Bold(Context.Guild.GetRole(entry.RoleId)?.Name ?? "MISSING_ROLE"))).WithIsInline(true))
                            .AddField(efb => efb.WithName(GetText("price")).WithValue(entry.Price.ToString()).WithIsInline(true))
                            .AddField(efb => efb.WithName(GetText("type")).WithValue(entry.Type.ToString()).WithIsInline(true));
                else if (entry.Type == ShopEntryType.List)
                    return embed.AddField(efb => efb.WithName(GetText("name")).WithValue(entry.Name).WithIsInline(true))
                            .AddField(efb => efb.WithName(GetText("price")).WithValue(entry.Price.ToString()).WithIsInline(true))
                            .AddField(efb => efb.WithName(GetText("type")).WithValue(GetText("random_unique_item")).WithIsInline(true));
                //else if (entry.Type == ShopEntryType.Infinite_List)
                //    return embed.AddField(efb => efb.WithName(GetText("name")).WithValue(GetText("shop_role", Format.Bold(entry.RoleName))).WithIsInline(true))
                //            .AddField(efb => efb.WithName(GetText("price")).WithValue(entry.Price.ToString()).WithIsInline(true))
                //            .AddField(efb => efb.WithName(GetText("type")).WithValue(entry.Type.ToString()).WithIsInline(true));
                else return null;
            }

            public string EntryToString(ShopEntry entry)
            {
                if (entry.Type == ShopEntryType.Role)
                {
                    return GetText("shop_role", Format.Bold(Context.Guild.GetRole(entry.RoleId)?.Name ?? "MISSING_ROLE"));
                }
                else if (entry.Type == ShopEntryType.List)
                {
                    return GetText("unique_items_left", entry.Items.Count) + "\n" + entry.Name;
                }
                //else if (entry.Type == ShopEntryType.Infinite_List)
                //{

                //}
                return "";
            }
        }
    }
}
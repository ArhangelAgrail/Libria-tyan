using Discord;
using NadekoBot.Common.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using System.Threading.Tasks;
using Wof = NadekoBot.Modules.Gambling.Common.WheelOfFortune.WheelOfFortuneGame;
using Image = SixLabors.ImageSharp.Image;
using NadekoBot.Modules.Gambling.Services;
using NadekoBot.Core.Modules.Gambling.Common;
using NadekoBot.Core.Common;
using System.Collections.Immutable;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.Primitives;
using System.Collections.Generic;

namespace NadekoBot.Modules.Gambling
{
    public partial class Gambling
    {
        public class WheelOfFortuneCommands : GamblingSubmodule<GamblingService>
        {
            private static readonly ImmutableArray<string> _emojis = new string[] {
            "⬆",
            "↖",
            "⬅",
            "↙",
            "⬇",
            "↘",
            "➡",
            "↗" }.ToImmutableArray();

            private readonly ICurrencyService _cs;
            private readonly DbService _db;
            private readonly IImageCache _images;

            private static readonly HashSet<ulong> _runningUsers = new HashSet<ulong>();

            public WheelOfFortuneCommands(IDataCache data, ICurrencyService cs, DbService db)
            {
                _images = data.LocalImages;
                _cs = cs;
                _db = db;
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task WheelOfFortune(ShmartNumber amount)
            {
                if (!await CheckBetMandatory(amount).ConfigureAwait(false))
                    return;

                if (!await _cs.RemoveAsync(Context.User.Id, "Wheel Of Fortune - bet", amount, gamble: true).ConfigureAwait(false))
                {
                    await ReplyErrorLocalized("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                    return;
                }

                var result = await _service.WheelOfFortuneSpinAsync(Context.User.Id, amount).ConfigureAwait(false);

                await Context.Channel.SendConfirmAsync(
Format.Bold($@"{Context.User.Mention} won: {result.Amount + Bc.BotConfig.CurrencySign}

   『{Wof.Multipliers[1]}』   『{Wof.Multipliers[0]}』   『{Wof.Multipliers[7]}』

『{Wof.Multipliers[2]}』      {_emojis[result.Index]}      『{Wof.Multipliers[6]}』

     『{Wof.Multipliers[3]}』   『{Wof.Multipliers[4]}』   『{Wof.Multipliers[5]}』")).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task WheelOfFortuneVisual(ShmartNumber amount)
            {
                if (!_runningUsers.Add(Context.User.Id))
                    return;
                try
                {

                    if (!await CheckBetMandatory(amount).ConfigureAwait(false))
                        return;

                    if (!await _cs.RemoveAsync(Context.User.Id, "Wheel Of Fortune - bet", amount, gamble: true).ConfigureAwait(false))
                    {
                        await ReplyErrorLocalized("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                        return;
                    }

                    var result = await _service.WheelOfFortuneSpinAsync(Context.User.Id, amount).ConfigureAwait(false);

                    using (var bgImage = Image.Load(_images.WheelBackground))
                    {
                        using (var Emoji = Image.Load(_images.WheelEmojis[result.Index]))
                        {
                            bgImage.Mutate(x => x.DrawImage(GraphicsOptions.Default, Emoji, new Point(0, 0)));

                            using (var CenterEmoji = Image.Load(_images.WheelEmojis[8]))
                            {
                                bgImage.Mutate(x => x.DrawImage(GraphicsOptions.Default, CenterEmoji, new Point(126, 124)));
                            }
                        }

                        using (var imgStream = bgImage.ToStream())
                        {
                            await Context.Channel.SendFileAsync(imgStream, "result.png", Context.User.Mention + $" {GetText("wheel_bet", result.Amount, Bc.BotConfig.CurrencySign)}\n{GetText("wheel_won", amount, Wof.Multipliers[result.Index], Bc.BotConfig.CurrencySign)}").ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    var _ = Task.Run(async () =>
                    {
                        await Task.Delay(1500).ConfigureAwait(false);
                        _runningUsers.Remove(Context.User.Id);
                    });
                }
            }
        }
    }
}
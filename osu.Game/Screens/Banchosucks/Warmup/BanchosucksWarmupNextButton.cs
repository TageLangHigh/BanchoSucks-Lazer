// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Screens.Banchosucks.Warmup
{
    /// <summary>
    /// Banchosucks: "next warm-up map (2/3)" on the results screen while a warm-up series runs.
    /// </summary>
    public partial class BanchosucksWarmupNextButton : RoundedButton
    {
        private readonly BanchosucksWarmupSeries series;

        public BanchosucksWarmupNextButton(BanchosucksWarmupSeries series)
        {
            this.series = series;

            Size = new Vector2(260, 30);

            // "2/3" is the map the click leads to
            Text = ClickerStrings.Text("Next warm-up map ({0}/{1})", "Nächste Aufwärm-Map ({0}/{1})", series.Index + 2, series.Maps.Count);
            Action = series.RequestNext;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            BackgroundColour = colours.Orange2;
        }
    }
}

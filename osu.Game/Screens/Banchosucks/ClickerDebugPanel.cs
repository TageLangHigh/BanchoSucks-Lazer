// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

#if CLICKER_DEBUG
using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: debug tools of the osu! Clicker, compiled only into test builds (CLICKER_DEBUG). F10 toggles it.
    /// </summary>
    internal partial class ClickerDebugPanel : CompositeDrawable
    {
        [Resolved]
        private ClickerEngine engine { get; set; } = null!;

        public Action? SpawnSpinner;
        public Action? SpawnSlider;

        public ClickerDebugPanel()
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            var buttons = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Padding = new MarginPadding(10),
            };

            buttons.Add(new OsuSpriteText
            {
                Text = "DEBUG (Testbuild) · F10",
                Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                Colour = colours.Yellow,
            });

            void add(string label, Action action, Color4? colour = null) => buttons.Add(new RoundedButton
            {
                Width = 220,
                Height = 28,
                Text = label,
                BackgroundColour = colour ?? Color4Extensions.FromHex("3a3f55"),
                Action = action,
            });

            add("+1 Mio PP", () => engine.DebugAddPoints(1e6));
            add("+1 Mrd PP", () => engine.DebugAddPoints(1e9));
            add("+1 Bio PP", () => engine.DebugAddPoints(1e12));
            add("+1 Brd PP", () => engine.DebugAddPoints(1e15));
            add("+1 Bio Lifetime (nur Prestige)", () => engine.DebugAddLifetime(1e12));
            add("+1 Brd Lifetime (nur Prestige)", () => engine.DebugAddLifetime(1e15));
            add("+10 Prestige-Punkte", () => engine.DebugAddPrestige(10), Color4Extensions.FromHex("6b3fa0"));
            add("+100 Prestige-Punkte", () => engine.DebugAddPrestige(100), Color4Extensions.FromHex("6b3fa0"));
            add("+1 Rebirth-Zähler", engine.DebugAddRebirth, Color4Extensions.FromHex("6b3fa0"));
            add("+10.000 Klicks", () => engine.DebugAddClicks(10_000));
            add("BPM-Rekord 300", () => engine.DebugSetBestBpm(300));
            add("Zeitsprung 1 h (offline, Cooldowns)", () => engine.DebugTimeSkip(1));
            add("Zeitsprung 8 h", () => engine.DebugTimeSkip(8));
            add("Encore (Map bestanden)", engine.DebugEncore);
            add("Spinner jetzt", () => SpawnSpinner?.Invoke());
            add("Slider jetzt", () => SpawnSlider?.Invoke());
            add("Spielstand zurücksetzen", engine.DebugReset, Color4Extensions.FromHex("8a2d31"));

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.8f },
                buttons,
            };
        }
    }
}
#endif

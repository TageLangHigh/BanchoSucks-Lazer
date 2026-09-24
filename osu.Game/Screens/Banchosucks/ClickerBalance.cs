// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// Banchosucks: numbers of the osu! Clicker.
    /// </summary>
    /// <remarks>
    /// The lazer server plugin <c>banchosucks_clicker</c> repeats these numbers to check leaderboard
    /// submissions. Change both together, otherwise honest players get rejected.
    /// </remarks>
    internal static class ClickerBalance
    {
        public const double COST_GROWTH = 1.15;
        public const double OFFLINE_RATE = 0.5;
        public const double OFFLINE_CAP_SECONDS = 8 * 3600;

        /// <summary>
        /// Taps further apart than this break the combo.
        /// </summary>
        public const double COMBO_TIMEOUT_MS = 1000;

        public static readonly ClickerProducer[] PRODUCERS =
        {
            new ClickerProducer("cursor", "Cursor-Trail", "Ein zweiter Cursor klickt ab und zu mit.", 15, 0.1, FontAwesome.Solid.MousePointer, Color4Extensions.FromHex("ff66ab")),
            new ClickerProducer("taiko", "Taiko-Trommel", "Don und Kat, ganz von allein.", 100, 1, FontAwesome.Solid.Drum, Color4Extensions.FromHex("ff5a5a")),
            new ClickerProducer("catch", "Obstkorb", "Fängt Früchte und damit PP.", 1_100, 8, FontAwesome.Solid.AppleAlt, Color4Extensions.FromHex("7fd13b")),
            new ClickerProducer("mania", "Mania-Tastatur", "Sieben Tasten, null Pause.", 12_000, 47, FontAwesome.Solid.Keyboard, Color4Extensions.FromHex("a06cd5")),
            new ClickerProducer("mapper", "Mapper", "Mappt rund um die Uhr neue Farm-Maps.", 130_000, 260, FontAwesome.Solid.PencilAlt, Color4Extensions.FromHex("4fc3f7")),
            new ClickerProducer("nominator", "Nominator", "Rankt Maps am Fließband.", 1_400_000, 1_400, FontAwesome.Solid.CheckCircle, Color4Extensions.FromHex("66bb6a")),
            new ClickerProducer("tournament", "Turnier", "Ganze Teams spielen für dich.", 20_000_000, 7_800, FontAwesome.Solid.Trophy, Color4Extensions.FromHex("ffc107")),
            new ClickerProducer("server", "Banchosucks-Server", "Ein eigener Server nur für deine PP.", 330_000_000, 44_000, FontAwesome.Solid.Server, Color4Extensions.FromHex("e5484d")),
        };

        public static readonly ClickerUpgrade[] UPGRADES =
        {
            new ClickerUpgrade("tablet", "Grafiktablett", "Klicks bringen doppelt so viel.", 100, FontAwesome.Solid.PenNib, Click: 2),
            new ClickerUpgrade("relax", "Relax", "Jeder Klick bringt zusätzlich 1 % deiner PP pro Sekunde.", 10_000, FontAwesome.Solid.Couch, "RX", ClickShare: 0.01),
            new ClickerUpgrade("hardrock", "Hard Rock", "Klicks bringen dreimal so viel.", 50_000, FontAwesome.Solid.Mountain, "HR", Click: 3),
            new ClickerUpgrade("doubletime", "Double Time", "Alle Gebäude produzieren doppelt.", 200_000, FontAwesome.Solid.Forward, "DT", Production: 2),
            new ClickerUpgrade("hidden", "Hidden", "Alle Gebäude produzieren noch einmal doppelt.", 5_000_000, FontAwesome.Solid.EyeSlash, "HD", Production: 2),
            new ClickerUpgrade("flashlight", "Flashlight", "Jeder Klick bringt zusätzlich 5 % deiner PP pro Sekunde.", 50_000_000, FontAwesome.Solid.Lightbulb, "FL", ClickShare: 0.05),
            new ClickerUpgrade("perfect", "Perfect", "Alle Gebäude produzieren dreimal so viel.", 500_000_000, FontAwesome.Solid.Crown, "PF", Production: 3),
        };

        /// <summary>
        /// osu!'s default combo colours, used for the "+PP" popups and hit bursts.
        /// </summary>
        public static readonly Color4[] COMBO_COLOURS =
        {
            Color4Extensions.FromHex("ffc000"),
            Color4Extensions.FromHex("00ca00"),
            Color4Extensions.FromHex("127cff"),
            Color4Extensions.FromHex("f21839"),
        };
    }

    internal record ClickerProducer(string Id, string Name, string Description, double BaseCost, double PerSecond, IconUsage Icon, Color4 Colour);

    internal record ClickerUpgrade(string Id, string Name, string Description, double Cost, IconUsage Icon, string? ModAcronym = null,
                                   double Click = 1, double Production = 1, double ClickShare = 0);

    /// <summary>
    /// What the osu! Clicker saves to <c>banchosucks/osu-clicker.json</c>.
    /// </summary>
    internal class ClickerState
    {
        public double Points { get; set; }
        public double TotalEarned { get; set; }
        public long Clicks { get; set; }
        public Dictionary<string, int> Producers { get; set; } = new Dictionary<string, int>();
        public HashSet<string> Upgrades { get; set; } = new HashSet<string>();
        public long SavedAt { get; set; }
        public double BestBpm { get; set; }
        public int BestCombo { get; set; }
    }

    internal static class ClickerFormat
    {
        private static readonly string[] units = { "Mio", "Mrd", "Bio", "Brd", "Trio", "Trd", "Quad" };

        /// <summary>
        /// Short German number: 12.345, 1,23 Mio, 4,5 Mrd and so on.
        /// </summary>
        public static string Number(double value)
        {
            if (value < 10)
                return value.ToString("0.#");
            if (value < 1_000_000)
                return Math.Floor(value).ToString("N0");

            int unit = -1;
            value /= 1000;

            while (value >= 1000 && unit < units.Length - 1)
            {
                value /= 1000;
                unit++;
            }

            return unit < 0 ? $"{value:0.##} Tsd" : $"{value:0.##} {units[unit]}";
        }
    }
}

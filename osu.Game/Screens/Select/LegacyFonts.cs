// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Central font provider for the legacy song select interface.
    /// Uses the default UI font unless <see cref="UseInterFont"/> is enabled.
    /// </summary>
    public static class LegacyFonts
    {
        /// <summary>
        /// Whether to use Inter instead of the default UI font.
        /// Updated by <see cref="OsuGameBase"/> using the existing <c>ToriiLegacyFont</c> preference.
        /// Changes apply when song select is next opened.
        /// </summary>
        public static bool UseInterFont { get; set; }

        public static FontUsage Get(float size, FontWeight weight = FontWeight.Regular)
            => UseInterFont
                ? OsuFont.GetFont(Typeface.Inter, size: size, weight: weight)
                : OsuFont.GetFont(size: size, weight: weight);
    }
}

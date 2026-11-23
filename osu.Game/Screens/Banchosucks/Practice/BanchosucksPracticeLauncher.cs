// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Screens.Select;

namespace osu.Game.Screens.Banchosucks.Practice
{
    /// <summary>
    /// Banchosucks: "retry from the miss" on the fail overlay.
    /// </summary>
    public static class BanchosucksPracticeLauncher
    {
        /// <summary>
        /// Leaves the failed play the way Quit does, back to song select, and opens the practice setup there with the
        /// section from shortly before <paramref name="failTime"/> to the end of the map. The setup starts the run right away.
        /// </summary>
        /// <param name="performer">Walks back through the screens (the game).</param>
        /// <param name="beatmap">The difficulty that was being played.</param>
        /// <param name="failTime">Map time of the fail.</param>
        public static void StartFromFail(IPerformFromScreenRunner performer, BeatmapInfo beatmap, double failTime)
        {
            performer.PerformFromScreen(screen =>
            {
                // The section belongs to one difficulty. If something changed the selection on the way back, do nothing rather than practise the wrong map.
                if (screen is not OsuScreen osuScreen || !osuScreen.Beatmap.Value.BeatmapInfo.Equals(beatmap))
                {
                    Logger.Log("Practice from the fail point was dropped because the selected beatmap changed.");
                    return;
                }

                screen.Push(BanchosucksPracticeSetupScreen.FromFail(failTime));
            }, new[] { typeof(SoloSongSelect) });
        }
    }
}

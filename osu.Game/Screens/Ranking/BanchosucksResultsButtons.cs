// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Banchosucks: the buttons this fork adds to the results screen's button row. One place for both looks:
    /// the normal bottom bar of <see cref="ResultsScreen"/> and the floating bar of <see cref="LegacyRankingOverlay"/>.
    /// </summary>
    public static class BanchosucksResultsButtons
    {
        /// <summary>
        /// Adds the buttons to a button row.
        /// </summary>
        /// <param name="buttons">The row.</param>
        /// <param name="selectedScore">The score the results screen currently shows.</param>
        public static void AddTo(FillFlowContainer buttons, Bindable<ScoreInfo?> selectedScore)
        {
            // no anchors here: every child of a fill flow has to use the same one as the stock buttons
            buttons.Add(new BanchosucksScoreCardButton
            {
                Score = { BindTarget = selectedScore },
            });

            buttons.Add(new BanchosucksRenderVideoButton
            {
                Score = { BindTarget = selectedScore },
            });
        }
    }
}

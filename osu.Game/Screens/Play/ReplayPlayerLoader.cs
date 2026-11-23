// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Screens;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play
{
    public partial class ReplayPlayerLoader : PlayerLoader
    {
        public readonly ScoreInfo Score;

        // Banchosucks: optionally starts the replay shortly before a miss (miss review on the results screen).
        public ReplayPlayerLoader(Score score, double? banchosucksMissTime = null)
            : base(createPlayer(score, banchosucksMissTime))
        {
            if (score.Replay == null)
                throw new ArgumentException($"{nameof(score)} must have a non-null {nameof(score.Replay)}.", nameof(score));

            Score = score.ScoreInfo;
            WindowShouldBeActiveForGameplayStart = false;
        }

        private static Func<Player> createPlayer(Score score, double? banchosucksMissTime)
        {
            // only the first player jumps to the miss; a restart of the replay begins at the start as usual
            double? missTime = banchosucksMissTime;

            return () =>
            {
                var player = new ReplayPlayer(score) { BanchosucksInitialMissTime = missTime };
                missTime = null;
                return player;
            };
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            // these will be reverted thanks to PlayerLoader's lease.
            Mods.Value = Score.Mods;
            Ruleset.Value = Score.Ruleset;

            base.OnEntering(e);
        }
    }
}

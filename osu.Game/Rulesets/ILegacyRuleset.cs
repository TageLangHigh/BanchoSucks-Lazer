// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring.Legacy;

namespace osu.Game.Rulesets
{
    public interface ILegacyRuleset
    {
        const int MAX_LEGACY_RULESET_ID = 3000;

        /// <summary>
        /// Identifies the server-side ID of a legacy ruleset.
        /// </summary>
        int LegacyID { get; }

        ILegacyScoreSimulator CreateLegacyScoreSimulator();
    }
}

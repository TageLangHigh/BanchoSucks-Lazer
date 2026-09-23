// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Localisation.Osu;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModSuddenDeath : ModSuddenDeath
    {
        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[]
        {
            typeof(OsuModTargetPractice),
        }).ToArray();

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.SuddenDeathAlsoFailSliderTailLabel))]
        public BindableBool FailOnSliderTail { get; } = new BindableBool();

        protected override bool FailCondition(HealthProcessor healthProcessor, JudgementResult result)
        {
            if (base.FailCondition(healthProcessor, result))
                return true;

            if (FailOnSliderTail.Value && result.HitObject is SliderTailCircle && !result.IsHit)
                return true;

            return false;
        }
    }
}

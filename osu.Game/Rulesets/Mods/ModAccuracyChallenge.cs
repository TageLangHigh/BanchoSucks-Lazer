// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Localisation.HUD;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mods
{
    public class ModAccuracyChallenge : ModFailCondition, IApplicableToScoreProcessor
    {
        public override string Name => "Accuracy Challenge";

        public override string Acronym => "AC";

        public override LocalisableString Description => CommonModsStrings.AccuracyChallengeDescription;

        public override IconUsage? Icon => OsuIcon.ModAccuracyChallenge;

        public override ModType Type => ModType.DifficultyIncrease;

        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[] { typeof(ModEasyWithExtraLives), typeof(ModPerfect) }).ToArray();

        public override bool RequiresConfiguration => false;

        public override bool Ranked => true;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!MinimumAccuracy.IsDefault)
                    yield return (CommonModsStrings.MinimumAccuracyLabel, MinimumAccuracy.Value.ToLocalisableString(@"P1"));

                if (!AccuracyJudgeMode.IsDefault)
                    yield return (CommonModsStrings.AccuracyModeLabel, AccuracyJudgeMode.Value.ToLocalisableString());

                if (!Restart.IsDefault)
                    yield return (CommonModsStrings.RestartOnFailLabel, CommonModsStrings.OnStateLabel);
            }
        }

        [SettingSource(typeof(CommonModsStrings), nameof(CommonModsStrings.MinimumAccuracyLabel), nameof(CommonModsStrings.MinimumAccuracyDescription), SettingControlType = typeof(MinimumAccuracySlider))]
        public BindableNumber<double> MinimumAccuracy { get; } = new BindableDouble
        {
            MinValue = 0.60,
            MaxValue = 0.999,
            Precision = 0.001,
            Default = 0.9,
            Value = 0.9,
        };

        [SettingSource(typeof(CommonModsStrings), nameof(CommonModsStrings.AccuracyModeLabel), nameof(CommonModsStrings.AccuracyModeDescription))]
        public Bindable<AccuracyMode> AccuracyJudgeMode { get; } = new Bindable<AccuracyMode>();

        private readonly Bindable<double> currentAccuracy = new Bindable<double>();

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            switch (AccuracyJudgeMode.Value)
            {
                case AccuracyMode.Standard:
                    currentAccuracy.BindTo(scoreProcessor.Accuracy);
                    break;

                case AccuracyMode.MaximumAchievable:
                    currentAccuracy.BindTo(scoreProcessor.MaximumAccuracy);
                    break;
            }

            currentAccuracy.BindValueChanged(s =>
            {
                if (s.NewValue < MinimumAccuracy.Value)
                {
                    TriggerFailure();
                }
            });
        }

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy) => rank;

        protected override bool FailCondition(HealthProcessor healthProcessor, JudgementResult result) => false;

        public enum AccuracyMode
        {
            [LocalisableDescription(typeof(GameplayAccuracyCounterStrings), nameof(GameplayAccuracyCounterStrings.AccuracyDisplayModeMax))]
            MaximumAchievable,

            [LocalisableDescription(typeof(GameplayAccuracyCounterStrings), nameof(GameplayAccuracyCounterStrings.AccuracyDisplayModeStandard))]
            Standard,
        }
    }

    public partial class MinimumAccuracySlider : SettingsPercentageSlider<double>
    {
        public MinimumAccuracySlider()
        {
            KeyboardStep = 0.001f;
        }
    }
}

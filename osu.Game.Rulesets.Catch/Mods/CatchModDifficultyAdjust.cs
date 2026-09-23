// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Localisation.Catch;
using osu.Game.Localisation;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Mods
{
    public class CatchModDifficultyAdjust : ModDifficultyAdjust, IApplicableToBeatmapProcessor
    {
        [SettingSource(typeof(SongSelectStrings), nameof(SongSelectStrings.CircleSize), null, FIRST_SETTING_ORDER - 1, SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable CircleSize { get; } = new DifficultyBindable
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 11,
            ReadCurrentFromDifficulty = diff => diff.CircleSize,
        };

        [SettingSource(typeof(SongSelectStrings), nameof(SongSelectStrings.ApproachRate), null, LAST_SETTING_ORDER + 1, SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable ApproachRate { get; } = new DifficultyBindable
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 11,
            ReadCurrentFromDifficulty = diff => diff.ApproachRate,
        };

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.DifficultyAdjustSpicyPatternsLabel), nameof(ModsStrings.DifficultyAdjustSpicyPatternsDescription))]
        public BindableBool HardRockOffsets { get; } = new BindableBool();

        public override string ExtendedIconInformation
        {
            get
            {
                if (!IsExactlyOneSettingChanged(CircleSize, ApproachRate, OverallDifficulty, DrainRate))
                    return string.Empty;

                if (!CircleSize.IsDefault) return format("CS", CircleSize);
                if (!ApproachRate.IsDefault) return format("AR", ApproachRate);
                if (!OverallDifficulty.IsDefault) return format("OD", OverallDifficulty);
                if (!DrainRate.IsDefault) return format("HP", DrainRate);

                return string.Empty;

                string format(string acronym, DifficultyBindable bindable)
                    => $"{acronym}{bindable.Value!.Value.ToStandardFormattedString(1)}";
            }
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!CircleSize.IsDefault)
                    yield return (SongSelectStrings.CircleSize, $"{CircleSize.Value:N1}");

                foreach (var setting in base.SettingDescription)
                    yield return setting;

                if (!ApproachRate.IsDefault)
                    yield return (SongSelectStrings.ApproachRate, $"{ApproachRate.Value:N1}");

                if (!HardRockOffsets.IsDefault)
                    yield return (ModsStrings.DifficultyAdjustSpicyPatternsLabel, CommonModsStrings.OnStateLabel);
            }
        }

        protected override void ApplySettings(BeatmapDifficulty difficulty)
        {
            base.ApplySettings(difficulty);

            if (CircleSize.Value != null) difficulty.CircleSize = CircleSize.Value.Value;
            if (ApproachRate.Value != null) difficulty.ApproachRate = ApproachRate.Value.Value;
        }

        public void ApplyToBeatmapProcessor(IBeatmapProcessor beatmapProcessor)
        {
            var catchProcessor = (CatchBeatmapProcessor)beatmapProcessor;
            catchProcessor.HardRockOffsets = HardRockOffsets.Value;
        }
    }
}

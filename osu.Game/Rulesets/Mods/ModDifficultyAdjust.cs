// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModDifficultyAdjust : Mod, IApplicableToDifficulty
    {
        public override string Name => @"Difficulty Adjust";

        public override LocalisableString Description => CommonModsStrings.DifficultyAdjustDescription;

        public override string Acronym => "DA";

        public override ModType Type => ModType.Conversion;

        public override IconUsage? Icon => OsuIcon.ModDifficultyAdjust;

        public override bool RequiresConfiguration => true;

        public override bool ValidForFreestyleAsRequiredMod => true;

        public override Type[] IncompatibleMods => new[] { typeof(ModEasy), typeof(ModHardRock) };

        protected const int FIRST_SETTING_ORDER = 1;

        protected const int LAST_SETTING_ORDER = 2;

        [SettingSource(typeof(SongSelectStrings), nameof(SongSelectStrings.HPDrain), null, FIRST_SETTING_ORDER, SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public DifficultyBindable DrainRate { get; } = new DifficultyBindable
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 11,
            ReadCurrentFromDifficulty = diff => diff.DrainRate,
        };

        [SettingSource(typeof(SongSelectStrings), nameof(SongSelectStrings.Accuracy), null, LAST_SETTING_ORDER, SettingControlType = typeof(DifficultyAdjustSettingsControl))]
        public virtual DifficultyBindable OverallDifficulty { get; } = new DifficultyBindable
        {
            Precision = 0.1f,
            MinValue = 0,
            MaxValue = 10,
            ExtendedMaxValue = 11,
            ReadCurrentFromDifficulty = diff => diff.OverallDifficulty,
        };

        [SettingSource(typeof(CommonModsStrings), nameof(CommonModsStrings.ExtendedLimitsLabel), nameof(CommonModsStrings.ExtendedLimitsDescription))]
        public BindableBool ExtendedLimits { get; } = new BindableBool();

        protected ModDifficultyAdjust()
        {
            foreach (var (_, property) in this.GetOrderedSettingsSourceProperties())
            {
                if (property.GetValue(this) is DifficultyBindable diffAdjustBindable)
                    diffAdjustBindable.ExtendedLimits.BindTo(ExtendedLimits);
            }
        }

        public override string ExtendedIconInformation
        {
            get
            {
                if (!IsExactlyOneSettingChanged(OverallDifficulty, DrainRate))
                    return string.Empty;

                if (!OverallDifficulty.IsDefault) return format("OD", OverallDifficulty);
                if (!DrainRate.IsDefault) return format("HP", DrainRate);

                return string.Empty;

                string format(string acronym, DifficultyBindable bindable) => $"{acronym}{bindable.Value!.Value.ToStandardFormattedString(1)}";
            }
        }

        protected bool IsExactlyOneSettingChanged(params DifficultyBindable[] difficultySettings)
        {
            DifficultyBindable? changedSetting = null;

            foreach (var setting in difficultySettings)
            {
                if (setting.IsDefault)
                    continue;

                if (changedSetting != null)
                    return false;

                changedSetting = setting;
            }

            return changedSetting != null;
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!DrainRate.IsDefault)
                    yield return (SongSelectStrings.HPDrain, $"{DrainRate.Value:N1}");

                if (!OverallDifficulty.IsDefault)
                    yield return (SongSelectStrings.Accuracy, $"{OverallDifficulty.Value:N1}");
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty) => ApplySettings(difficulty);

        /// <summary>
        /// Apply all custom settings to the provided beatmap.
        /// </summary>
        /// <param name="difficulty">The beatmap to have settings applied.</param>
        protected virtual void ApplySettings(BeatmapDifficulty difficulty)
        {
            if (DrainRate.Value != null) difficulty.DrainRate = DrainRate.Value.Value;
            if (OverallDifficulty.Value != null) difficulty.OverallDifficulty = OverallDifficulty.Value.Value;
        }
    }
}

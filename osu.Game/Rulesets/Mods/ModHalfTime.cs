// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModHalfTime : ModRateAdjust
    {
        public override string Name => "Half Time";
        public override string Acronym => "HT";
        public override IconUsage? Icon => OsuIcon.ModHalfTime;
        public override ModType Type => ModType.DifficultyReduction;
        public override LocalisableString Description => CommonModsStrings.HalfTimeDescription;
        public override bool Ranked => SpeedChange.IsDefault;

        [SettingSource(typeof(CommonModsStrings), nameof(CommonModsStrings.SpeedDecreaseLabel), nameof(CommonModsStrings.SpeedDecreaseDescription), SettingControlType = typeof(MultiplierSettingsSlider))]
        public override BindableNumber<double> SpeedChange { get; } = new BindableDouble(0.75)
        {
            MinValue = 0.5,
            MaxValue = 0.99,
            Precision = 0.01,
        };

        [SettingSource(typeof(CommonModsStrings), nameof(CommonModsStrings.AdjustPitchLabel), nameof(CommonModsStrings.AdjustPitchDescription))]
        public virtual BindableBool AdjustPitch { get; } = new BindableBool();

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                foreach (var description in base.SettingDescription)
                    yield return description;

                if (!AdjustPitch.IsDefault)
                    yield return (CommonModsStrings.AdjustPitchLabel, LocalisationUtils.BooleanStateLabel(AdjustPitch.Value));
            }
        }

        private readonly RateAdjustModHelper rateAdjustHelper;

        protected ModHalfTime()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);
        }

        public override void ApplyToTrack(IAdjustableAudioComponent track)
        {
            rateAdjustHelper.ApplyToTrack(track);
        }
    }
}

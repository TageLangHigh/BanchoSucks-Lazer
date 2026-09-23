// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModEasyWithExtraLives : ModEasy, IApplicableFailOverride, IApplicableToPlayer, IApplicableToHealthProcessor
    {
        [SettingSource(typeof(CommonModsStrings), nameof(CommonModsStrings.ExtraLivesLabel), nameof(CommonModsStrings.ExtraLivesDescription))]
        public Bindable<int> Retries { get; } = new BindableInt(2)
        {
            MinValue = 0,
            MaxValue = 10
        };

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!Retries.IsDefault)
                    yield return (CommonModsStrings.ExtraLivesLabel, Retries.Value.ToString());
            }
        }

        public override Type[] IncompatibleMods => base.IncompatibleMods.Append(typeof(ModAccuracyChallenge)).ToArray();

        private int? retries;

        private readonly BindableNumber<double> health = new BindableDouble();

        public void ApplyToPlayer(Player player)
        {
            // this throw works for two reasons:
            // - every time `Player` loads, it deep-clones mods into itself, and the deep clone copies *only* `[SettingsSource]` properties
            // - `Player` is the only consumer of `IApplicableToPlayer` and it calls `ApplyToPlayer()` exactly once per mod instance
            // if either of the above assumptions no longer holds true for any reason, this will need to be reconsidered
            if (retries != null)
                throw new InvalidOperationException(@"Cannot apply this mod instance to a player twice.");

            retries = Retries.Value;
        }

        public bool PerformFail()
        {
            Debug.Assert(retries != null);

            if (retries == 0) return true;

            health.Value = health.MaxValue;
            retries--;

            return false;
        }

        public bool RestartOnFail => false;

        public void ApplyToHealthProcessor(HealthProcessor healthProcessor)
        {
            health.BindTo(healthProcessor.Health);
        }
    }
}

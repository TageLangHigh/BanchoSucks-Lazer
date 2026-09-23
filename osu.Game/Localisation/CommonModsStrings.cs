// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class CommonModsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.CommonMods";

        /// <summary>
        /// "Let track speed adapt to you."
        /// </summary>
        public static LocalisableString AdaptiveSpeedDescription => new TranslatableString(getKey(@"adaptive_speed_description"), @"Let track speed adapt to you.");

        /// <summary>
        /// "Watch a perfect automated play through the song."
        /// </summary>
        public static LocalisableString AutoplayDescription => new TranslatableString(getKey(@"autoplay_description"), @"Watch a perfect automated play through the song.");

        /// <summary>
        /// "Fail if your accuracy drops too low!"
        /// </summary>
        public static LocalisableString AccuracyChallengeDescription => new TranslatableString(getKey(@"accuracy_challenge_description"), @"Fail if your accuracy drops too low!");

        /// <summary>
        /// "The whole playfield is on a wheel!"
        /// </summary>
        public static LocalisableString BarrelRollDescription => new TranslatableString(getKey(@"barrel_roll_description"), @"The whole playfield is on a wheel!");

        /// <summary>
        /// "Feeling nostalgic?"
        /// </summary>
        public static LocalisableString ClassicDescription => new TranslatableString(getKey(@"classic_description"), @"Feeling nostalgic?");

        /// <summary>
        /// "Watch the video without visual distractions."
        /// </summary>
        public static LocalisableString CinemaDescription => new TranslatableString(getKey(@"cinema_description"), @"Watch the video without visual distractions.");

        /// <summary>
        /// "No more tricky speed changes!"
        /// </summary>
        public static LocalisableString ConstantSpeedDescription => new TranslatableString(getKey(@"constant_speed_description"), @"No more tricky speed changes!");

        /// <summary>
        /// "Whoaaaaa..."
        /// </summary>
        public static LocalisableString DaycoreDescription => new TranslatableString(getKey(@"daycore_description"), @"Whoaaaaa...");

        /// <summary>
        /// "Override a beatmap's difficulty settings."
        /// </summary>
        public static LocalisableString DifficultyAdjustDescription => new TranslatableString(getKey(@"difficulty_adjust_description"), @"Override a beatmap's difficulty settings.");

        /// <summary>
        /// "Zoooooooooom..."
        /// </summary>
        public static LocalisableString DoubleTimeDescription => new TranslatableString(getKey(@"double_time_description"), @"Zoooooooooom...");

        /// <summary>
        /// "Restricted view area."
        /// </summary>
        public static LocalisableString FlashlightDescription => new TranslatableString(getKey(@"flashlight_description"), @"Restricted view area.");

        /// <summary>
        /// "Less zoom..."
        /// </summary>
        public static LocalisableString HalfTimeDescription => new TranslatableString(getKey(@"half_time_description"), @"Less zoom...");

        /// <summary>
        /// "Everything just got a bit harder..."
        /// </summary>
        public static LocalisableString HardRockDescription => new TranslatableString(getKey(@"hard_rock_description"), @"Everything just got a bit harder...");

        /// <summary>
        /// "Can you still feel the rhythm without music?"
        /// </summary>
        public static LocalisableString MutedDescription => new TranslatableString(getKey(@"muted_description"), @"Can you still feel the rhythm without music?");

        /// <summary>
        /// "Uguuuuuuuu..."
        /// </summary>
        public static LocalisableString NightcoreDescription => new TranslatableString(getKey(@"nightcore_description"), @"Uguuuuuuuu...");

        /// <summary>
        /// "You can't fail, no matter what."
        /// </summary>
        public static LocalisableString NoFailDescription => new TranslatableString(getKey(@"no_fail_description"), @"You can't fail, no matter what.");

        /// <summary>
        /// "No mods applied."
        /// </summary>
        public static LocalisableString NoModDescription => new TranslatableString(getKey(@"no_mod_description"), @"No mods applied.");

        /// <summary>
        /// "SS or quit."
        /// </summary>
        public static LocalisableString PerfectDescription => new TranslatableString(getKey(@"perfect_description"), @"SS or quit.");

        /// <summary>
        /// "Score set on earlier g0v0! versions with the V2 scoring algorithm active."
        /// </summary>
        public static LocalisableString ScoreV2Description => new TranslatableString(getKey(@"score_v2_description"), @"Score set on earlier g0v0! versions with the V2 scoring algorithm active.");

        /// <summary>
        /// "Miss and fail."
        /// </summary>
        public static LocalisableString SuddenDeathDescription => new TranslatableString(getKey(@"sudden_death_description"), @"Miss and fail.");

        /// <summary>
        /// "Colours hit objects based on the rhythm."
        /// </summary>
        public static LocalisableString SynesthesiaDescription => new TranslatableString(getKey(@"synesthesia_description"), @"Colours hit objects based on the rhythm.");

        /// <summary>
        /// "Automatically applied to plays on devices with a touchscreen."
        /// </summary>
        public static LocalisableString TouchDeviceDescription => new TranslatableString(getKey(@"touch_device_description"), @"Automatically applied to plays on devices with a touchscreen.");

        /// <summary>
        /// "This mod could not be resolved by the game."
        /// </summary>
        public static LocalisableString UnknownModDescription => new TranslatableString(getKey(@"unknown_mod_description"), @"This mod could not be resolved by the game.");

        /// <summary>
        /// "Sloooow doooown..."
        /// </summary>
        public static LocalisableString WindDownDescription => new TranslatableString(getKey(@"wind_down_description"), @"Sloooow doooown...");

        /// <summary>
        /// "Can you keep up?"
        /// </summary>
        public static LocalisableString WindUpDescription => new TranslatableString(getKey(@"wind_up_description"), @"Can you keep up?");

        /// <summary>
        /// "Initial rate"
        /// </summary>
        public static LocalisableString InitialRateLabel => new TranslatableString(getKey(@"initial_rate_label"), @"Initial rate");

        /// <summary>
        /// "The starting speed of the track"
        /// </summary>
        public static LocalisableString InitialRateDescription => new TranslatableString(getKey(@"initial_rate_description"), @"The starting speed of the track");

        /// <summary>
        /// "Adjust pitch"
        /// </summary>
        public static LocalisableString AdjustPitchLabel => new TranslatableString(getKey(@"adjust_pitch_label"), @"Adjust pitch");

        /// <summary>
        /// "Should pitch be adjusted with speed"
        /// </summary>
        public static LocalisableString AdjustPitchDescription => new TranslatableString(getKey(@"adjust_pitch_description"), @"Should pitch be adjusted with speed");

        /// <summary>
        /// "Minimum accuracy"
        /// </summary>
        public static LocalisableString MinimumAccuracyLabel => new TranslatableString(getKey(@"minimum_accuracy_label"), @"Minimum accuracy");

        /// <summary>
        /// "Trigger a failure if your accuracy goes below this value."
        /// </summary>
        public static LocalisableString MinimumAccuracyDescription => new TranslatableString(getKey(@"minimum_accuracy_description"), @"Trigger a failure if your accuracy goes below this value.");

        /// <summary>
        /// "Accuracy mode"
        /// </summary>
        public static LocalisableString AccuracyModeLabel => new TranslatableString(getKey(@"accuracy_mode_label"), @"Accuracy mode");

        /// <summary>
        /// "The mode of accuracy that will trigger failure."
        /// </summary>
        public static LocalisableString AccuracyModeDescription => new TranslatableString(getKey(@"accuracy_mode_description"), @"The mode of accuracy that will trigger failure.");

        /// <summary>
        /// "Roll speed"
        /// </summary>
        public static LocalisableString RollSpeedLabel => new TranslatableString(getKey(@"roll_speed_label"), @"Roll speed");

        /// <summary>
        /// "Rotations per minute"
        /// </summary>
        public static LocalisableString RollSpeedDescription => new TranslatableString(getKey(@"roll_speed_description"), @"Rotations per minute");

        /// <summary>
        /// "Direction"
        /// </summary>
        public static LocalisableString DirectionLabel => new TranslatableString(getKey(@"direction_label"), @"Direction");

        /// <summary>
        /// "The direction of rotation"
        /// </summary>
        public static LocalisableString DirectionDescription => new TranslatableString(getKey(@"direction_description"), @"The direction of rotation");

        /// <summary>
        /// "Speed decrease"
        /// </summary>
        public static LocalisableString SpeedDecreaseLabel => new TranslatableString(getKey(@"speed_decrease_label"), @"Speed decrease");

        /// <summary>
        /// "The actual decrease to apply"
        /// </summary>
        public static LocalisableString SpeedDecreaseDescription => new TranslatableString(getKey(@"speed_decrease_description"), @"The actual decrease to apply");

        /// <summary>
        /// "Extended Limits"
        /// </summary>
        public static LocalisableString ExtendedLimitsLabel => new TranslatableString(getKey(@"extended_limits_label"), @"Extended Limits");

        /// <summary>
        /// "Adjust difficulty beyond sane limits."
        /// </summary>
        public static LocalisableString ExtendedLimitsDescription => new TranslatableString(getKey(@"extended_limits_description"), @"Adjust difficulty beyond sane limits.");

        /// <summary>
        /// "Speed increase"
        /// </summary>
        public static LocalisableString SpeedIncreaseLabel => new TranslatableString(getKey(@"speed_increase_label"), @"Speed increase");

        /// <summary>
        /// "The actual increase to apply"
        /// </summary>
        public static LocalisableString SpeedIncreaseDescription => new TranslatableString(getKey(@"speed_increase_description"), @"The actual increase to apply");

        /// <summary>
        /// "Extra Lives"
        /// </summary>
        public static LocalisableString ExtraLivesLabel => new TranslatableString(getKey(@"extra_lives_label"), @"Extra Lives");

        /// <summary>
        /// "Number of extra lives"
        /// </summary>
        public static LocalisableString ExtraLivesDescription => new TranslatableString(getKey(@"extra_lives_description"), @"Number of extra lives");

        /// <summary>
        /// "Restart on fail"
        /// </summary>
        public static LocalisableString RestartOnFailLabel => new TranslatableString(getKey(@"restart_on_fail_label"), @"Restart on fail");

        /// <summary>
        /// "Automatically restarts when failed."
        /// </summary>
        public static LocalisableString RestartOnFailDescription => new TranslatableString(getKey(@"restart_on_fail_description"), @"Automatically restarts when failed.");

        /// <summary>
        /// "Flashlight size"
        /// </summary>
        public static LocalisableString FlashlightSizeLabel => new TranslatableString(getKey(@"flashlight_size_label"), @"Flashlight size");

        /// <summary>
        /// "Multiplier applied to the default flashlight size."
        /// </summary>
        public static LocalisableString FlashlightSizeDescription => new TranslatableString(getKey(@"flashlight_size_description"), @"Multiplier applied to the default flashlight size.");

        /// <summary>
        /// "Change size based on combo"
        /// </summary>
        public static LocalisableString FlashlightChangeSizeBasedOnComboLabel => new TranslatableString(getKey(@"flashlight_change_size_based_on_combo_label"), @"Change size based on combo");

        /// <summary>
        /// "Decrease the flashlight size as combo increases."
        /// </summary>
        public static LocalisableString FlashlightChangeSizeBasedOnComboDescription => new TranslatableString(getKey(@"flashlight_change_size_based_on_combo_description"), @"Decrease the flashlight size as combo increases.");

        /// <summary>
        /// "Start muted"
        /// </summary>
        public static LocalisableString StartMutedLabel => new TranslatableString(getKey(@"start_muted_label"), @"Start muted");

        /// <summary>
        /// "Increase volume as combo builds."
        /// </summary>
        public static LocalisableString StartMutedDescription => new TranslatableString(getKey(@"start_muted_description"), @"Increase volume as combo builds.");

        /// <summary>
        /// "Enable metronome"
        /// </summary>
        public static LocalisableString EnableMetronomeLabel => new TranslatableString(getKey(@"enable_metronome_label"), @"Enable metronome");

        /// <summary>
        /// "Add a metronome beat to help you keep track of the rhythm."
        /// </summary>
        public static LocalisableString EnableMetronomeDescription => new TranslatableString(getKey(@"enable_metronome_description"), @"Add a metronome beat to help you keep track of the rhythm.");

        /// <summary>
        /// "Final volume at combo"
        /// </summary>
        public static LocalisableString FinalVolumeAtComboLabel => new TranslatableString(getKey(@"final_volume_at_combo_label"), @"Final volume at combo");

        /// <summary>
        /// "The combo count at which point the track reaches its final volume."
        /// </summary>
        public static LocalisableString FinalVolumeAtComboDescription => new TranslatableString(getKey(@"final_volume_at_combo_description"), @"The combo count at which point the track reaches its final volume.");

        /// <summary>
        /// "Mute hit sounds"
        /// </summary>
        public static LocalisableString MuteHitSoundsLabel => new TranslatableString(getKey(@"mute_hit_sounds_label"), @"Mute hit sounds");

        /// <summary>
        /// "Hit sounds are also muted alongside the track."
        /// </summary>
        public static LocalisableString MuteHitSoundsDescription => new TranslatableString(getKey(@"mute_hit_sounds_description"), @"Hit sounds are also muted alongside the track.");

        /// <summary>
        /// "always muted"
        /// </summary>
        public static LocalisableString AlwaysMutedLabel => new TranslatableString(getKey(@"always_muted_label"), @"always muted");

        /// <summary>
        /// "Hidden at combo"
        /// </summary>
        public static LocalisableString HiddenAtComboLabel => new TranslatableString(getKey(@"hidden_at_combo_label"), @"Hidden at combo");

        /// <summary>
        /// "The combo count at which the cursor becomes completely hidden"
        /// </summary>
        public static LocalisableString HiddenAtComboDescription => new TranslatableString(getKey(@"hidden_at_combo_description"), @"The combo count at which the cursor becomes completely hidden");

        /// <summary>
        /// "always hidden"
        /// </summary>
        public static LocalisableString AlwaysHiddenLabel => new TranslatableString(getKey(@"always_hidden_label"), @"always hidden");

        /// <summary>
        /// "Speed change"
        /// </summary>
        public static LocalisableString SpeedChangeLabel => new TranslatableString(getKey(@"speed_change_label"), @"Speed change");

        /// <summary>
        /// "Seed"
        /// </summary>
        public static LocalisableString SeedLabel => new TranslatableString(getKey(@"seed_label"), @"Seed");

        /// <summary>
        /// "Use a custom seed instead of a random one"
        /// </summary>
        public static LocalisableString SeedDescription => new TranslatableString(getKey(@"seed_description"), @"Use a custom seed instead of a random one");

        /// <summary>
        /// "Final rate"
        /// </summary>
        public static LocalisableString FinalRateLabel => new TranslatableString(getKey(@"final_rate_label"), @"Final rate");

        /// <summary>
        /// "The final speed to ramp to"
        /// </summary>
        public static LocalisableString FinalRateDescription => new TranslatableString(getKey(@"final_rate_description"), @"The final speed to ramp to");

        /// <summary>
        /// "On"
        /// </summary>
        public static LocalisableString OnStateLabel => new TranslatableString(getKey(@"on_state_label"), @"On");

        /// <summary>
        /// "Off"
        /// </summary>
        public static LocalisableString OffStateLabel => new TranslatableString(getKey(@"off_state_label"), @"Off");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

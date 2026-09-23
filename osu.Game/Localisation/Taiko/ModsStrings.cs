// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation.Taiko
{
    public static class ModsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Taiko.Mods";

        /// <summary>
        /// "Beats move slower, and less accuracy required!"
        /// </summary>
        public static LocalisableString EasyDescription => new TranslatableString(getKey(@"easy_description"), @"Beats move slower, and less accuracy required!");

        /// <summary>
        /// "Beats fade out before you hit them!"
        /// </summary>
        public static LocalisableString HiddenDescription => new TranslatableString(getKey(@"hidden_description"), @"Beats fade out before you hit them!");

        /// <summary>
        /// "Shuffle around the colours!"
        /// </summary>
        public static LocalisableString RandomDescription => new TranslatableString(getKey(@"random_description"), @"Shuffle around the colours!");

        /// <summary>
        /// "No need to remember which key is correct anymore!"
        /// </summary>
        public static LocalisableString RelaxDescription => new TranslatableString(getKey(@"relax_description"), @"No need to remember which key is correct anymore!");

        /// <summary>
        /// "One key for dons, one key for kats."
        /// </summary>
        public static LocalisableString SingleTapDescription => new TranslatableString(getKey(@"single_tap_description"), @"One key for dons, one key for kats.");

        /// <summary>
        /// "Simplify tricky rhythms!"
        /// </summary>
        public static LocalisableString SimplifiedRhythmDescription => new TranslatableString(getKey(@"simplified_rhythm_description"), @"Simplify tricky rhythms!");

        /// <summary>
        /// "Dons become kats, kats become dons"
        /// </summary>
        public static LocalisableString SwapDescription => new TranslatableString(getKey(@"swap_description"), @"Dons become kats, kats become dons");

        /// <summary>
        /// "Scroll Speed"
        /// </summary>
        public static LocalisableString DifficultyAdjustScrollSpeedLabel => new TranslatableString(getKey(@"difficulty_adjust_scroll_speed_label"), @"Scroll Speed");

        /// <summary>
        /// "Adjust a beatmap's set scroll speed"
        /// </summary>
        public static LocalisableString DifficultyAdjustScrollSpeedDescription => new TranslatableString(getKey(@"difficulty_adjust_scroll_speed_description"), @"Adjust a beatmap's set scroll speed");

        /// <summary>
        /// "1/3 to 1/2 conversion"
        /// </summary>
        public static LocalisableString SimplifiedRhythm13To12ConversionLabel => new TranslatableString(getKey(@"simplified_rhythm_1_3_to_1_2_conversion_label"), @"1/3 to 1/2 conversion");

        /// <summary>
        /// "Converts 1/3 patterns to 1/2 rhythm."
        /// </summary>
        public static LocalisableString SimplifiedRhythm13To12ConversionDescription => new TranslatableString(getKey(@"simplified_rhythm_1_3_to_1_2_conversion_description"), @"Converts 1/3 patterns to 1/2 rhythm.");

        /// <summary>
        /// "1/6 to 1/4 conversion"
        /// </summary>
        public static LocalisableString SimplifiedRhythm16To14ConversionLabel => new TranslatableString(getKey(@"simplified_rhythm_1_6_to_1_4_conversion_label"), @"1/6 to 1/4 conversion");

        /// <summary>
        /// "Converts 1/6 patterns to 1/4 rhythm."
        /// </summary>
        public static LocalisableString SimplifiedRhythm16To14ConversionDescription => new TranslatableString(getKey(@"simplified_rhythm_1_6_to_1_4_conversion_description"), @"Converts 1/6 patterns to 1/4 rhythm.");

        /// <summary>
        /// "1/8 to 1/4 conversion"
        /// </summary>
        public static LocalisableString SimplifiedRhythm18To14ConversionLabel => new TranslatableString(getKey(@"simplified_rhythm_1_8_to_1_4_conversion_label"), @"1/8 to 1/4 conversion");

        /// <summary>
        /// "Converts 1/8 patterns to 1/4 rhythm."
        /// </summary>
        public static LocalisableString SimplifiedRhythm18To14ConversionDescription => new TranslatableString(getKey(@"simplified_rhythm_1_8_to_1_4_conversion_description"), @"Converts 1/8 patterns to 1/4 rhythm.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

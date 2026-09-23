// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation.Catch
{
    public static class ModsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Catch.Mods";

        /// <summary>
        /// "Larger fruits, more forgiving HP drain, less accuracy required, and extra lives!"
        /// </summary>
        public static LocalisableString EasyDescription => new TranslatableString(getKey(@"easy_description"),
            @"Larger fruits, more forgiving HP drain, less accuracy required, and extra lives!");

        /// <summary>
        /// "The fruits are... floating?"
        /// </summary>
        public static LocalisableString FloatingFruitsDescription => new TranslatableString(getKey(@"floating_fruits_description"), @"The fruits are... floating?");

        /// <summary>
        /// "Play with fading fruits."
        /// </summary>
        public static LocalisableString HiddenDescription => new TranslatableString(getKey(@"hidden_description"), @"Play with fading fruits.");

        /// <summary>
        /// "Fruits are flipped horizontally."
        /// </summary>
        public static LocalisableString MirrorDescription => new TranslatableString(getKey(@"mirror_description"), @"Fruits are flipped horizontally.");

        /// <summary>
        /// "Dashing by default, slow down!"
        /// </summary>
        public static LocalisableString MovingFastDescription => new TranslatableString(getKey(@"moving_fast_description"), @"Dashing by default, slow down!");

        /// <summary>
        /// "Where's the catcher?"
        /// </summary>
        public static LocalisableString NoScopeDescription => new TranslatableString(getKey(@"no_scope_description"), @"Where's the catcher?");

        /// <summary>
        /// "Use the mouse to control the catcher."
        /// </summary>
        public static LocalisableString RelaxDescription => new TranslatableString(getKey(@"relax_description"), @"Use the mouse to control the catcher.");

        /// <summary>
        /// "Spicy Patterns"
        /// </summary>
        public static LocalisableString DifficultyAdjustSpicyPatternsLabel => new TranslatableString(getKey(@"difficulty_adjust_spicy_patterns_label"), @"Spicy Patterns");

        /// <summary>
        /// "Adjust the patterns as if Hard Rock is enabled."
        /// </summary>
        public static LocalisableString DifficultyAdjustSpicyPatternsDescription => new TranslatableString(getKey(@"difficulty_adjust_spicy_patterns_description"), @"Adjust the patterns as if Hard Rock is enabled.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

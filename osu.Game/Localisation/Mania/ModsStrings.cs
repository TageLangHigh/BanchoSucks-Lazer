// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation.Mania
{
    public static class ModsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Mania.Mods";

        /// <summary>
        /// "Decrease the playfield's viewing area."
        /// </summary>
        public static LocalisableString CoverDescription => new TranslatableString(getKey(@"cover_description"), @"Decrease the playfield's viewing area.");

        /// <summary>
        /// "Double the stages, double the fun!"
        /// </summary>
        public static LocalisableString DualStagesDescription => new TranslatableString(getKey(@"dual_stages_description"), @"Double the stages, double the fun!");

        /// <summary>
        /// "More forgiving HP drain, less accuracy required, and extra lives!"
        /// </summary>
        public static LocalisableString EasyDescription => new TranslatableString(getKey(@"easy_description"), @"More forgiving HP drain, less accuracy required, and extra lives!");

        /// <summary>
        /// "Keys appear out of nowhere!"
        /// </summary>
        public static LocalisableString FadeInDescription => new TranslatableString(getKey(@"fade_in_description"), @"Keys appear out of nowhere!");

        /// <summary>
        /// "Keys fade out before you hit them!"
        /// </summary>
        public static LocalisableString HiddenDescription => new TranslatableString(getKey(@"hidden_description"), @"Keys fade out before you hit them!");

        /// <summary>
        /// "Replaces all hold notes with normal notes."
        /// </summary>
        public static LocalisableString HoldOffDescription => new TranslatableString(getKey(@"hold_off_description"), @"Replaces all hold notes with normal notes.");

        /// <summary>
        /// "Hold the keys. To the beat."
        /// </summary>
        public static LocalisableString InvertDescription => new TranslatableString(getKey(@"invert_description"), @"Hold the keys. To the beat.");

        /// <summary>
        /// "Play with one key."
        /// </summary>
        public static LocalisableString Key1Description => new TranslatableString(getKey(@"key_1_description"), @"Play with one key.");

        public static LocalisableString KeyModDescription(int count) => count == 1
            ? Key1Description
            : new TranslatableString(getKey(@"key_mod_description"), @"Play with {0} keys.", count);

        /// <summary>
        /// "Notes are flipped horizontally."
        /// </summary>
        public static LocalisableString MirrorDescription => new TranslatableString(getKey(@"mirror_description"), @"Notes are flipped horizontally.");

        /// <summary>
        /// "No more timing the end of hold notes."
        /// </summary>
        public static LocalisableString NoReleaseDescription => new TranslatableString(getKey(@"no_release_description"), @"No more timing the end of hold notes.");

        /// <summary>
        /// "Shuffle around the keys!"
        /// </summary>
        public static LocalisableString RandomDescription => new TranslatableString(getKey(@"random_description"), @"Shuffle around the keys!");

        /// <summary>
        /// "Coverage"
        /// </summary>
        public static LocalisableString CoverCoverageLabel => new TranslatableString(getKey(@"cover_coverage_label"), @"Coverage");

        /// <summary>
        /// "The proportion of playfield height that notes will be hidden for."
        /// </summary>
        public static LocalisableString CoverCoverageDescription => new TranslatableString(getKey(@"cover_coverage_description"), @"The proportion of playfield height that notes will be hidden for.");

        /// <summary>
        /// "The direction on which the cover is applied"
        /// </summary>
        public static LocalisableString CoverDirectionDescription => new TranslatableString(getKey(@"cover_direction_description"), @"The direction on which the cover is applied");

        /// <summary>
        /// "Require perfect hits"
        /// </summary>
        public static LocalisableString PerfectRequirePerfectHitsLabel => new TranslatableString(getKey(@"perfect_require_perfect_hits_label"), @"Require perfect hits");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

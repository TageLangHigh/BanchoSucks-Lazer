using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class DisclaimerStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Disclaimer";

        /// <summary>
        /// "this is "
        /// </summary>
        public static LocalisableString TitlePart => new TranslatableString(getKey(@"title_part"), @"this is ");

        /// <summary>
        /// "a free and open rhythm game based on "
        /// </summary>
        public static LocalisableString BasedOnParagraph => new TranslatableString(getKey(@"based_on_paragraph"), @"a free and open rhythm game based on ");

        /// <summary>
        /// " codebase originally developed by ppy Pty Ltd."
        /// </summary>
        public static LocalisableString OriginalCodeParagraph => new TranslatableString(getKey(@"original_code_paragraph"), @" codebase originally developed by ppy Pty Ltd.");

        /// <summary>
        /// "this is a community project and is not affiliated with ppy Pty Ltd or osu! in any way."
        /// </summary>
        public static LocalisableString CommunityProjectParagraph => new TranslatableString(getKey(@"community_project_paragraph"),
            @"this is a community project and is not affiliated with ppy Pty Ltd or osu! in any way.");

        /// <summary>
        /// "if you have any issue, please report at our GitHub:"
        /// </summary>
        public static LocalisableString ReportGitHubParagraph => new TranslatableString(getKey(@"report_github_paragraph"),
            @"if you have any issue, please report at our GitHub:");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

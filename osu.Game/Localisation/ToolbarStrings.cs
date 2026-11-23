// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ToolbarStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Toolbar";

        /// <summary>
        /// "Connection interrupted, will try to reconnect..."
        /// </summary>
        public static LocalisableString AttemptingToReconnect => new TranslatableString(getKey(@"attempting_to_reconnect"), @"Connection interrupted, will try to reconnect...");

        /// <summary>
        /// "Connecting..."
        /// </summary>
        public static LocalisableString Connecting => new TranslatableString(getKey(@"connecting"), @"Connecting...");

        /// <summary>
        /// "Verification required"
        /// </summary>
        public static LocalisableString VerificationRequired => new TranslatableString(getKey(@"verification_required"), @"Verification required");

        /// <summary>
        /// "home"
        /// </summary>
        public static LocalisableString HomeHeaderTitle => new TranslatableString(getKey(@"home_header_title"), @"home");

        /// <summary>
        /// "return to the main menu"
        /// </summary>
        public static LocalisableString HomeHeaderDescription => new TranslatableString(getKey(@"home_header_description"), @"return to the main menu");

        /// <summary>
        /// "play some {0}"
        /// </summary>
        public static LocalisableString PlaySomeRuleset(string arg0) => new TranslatableString(getKey(@"play_some_ruleset"), @"play some {0}", arg0);

        /// <summary>
        /// "running"
        /// </summary>
        public static LocalisableString TimeRunning => new TranslatableString(getKey(@"time_running"), @"running");

        /// <summary>
        /// "Toolbar"
        /// </summary>
        public static LocalisableString ToolbarSettingsHeader => new TranslatableString(getKey(@"toolbar_settings_header"), @"Toolbar");

        /// <summary>
        /// "more icons"
        /// </summary>
        public static LocalisableString MoreIcons => new TranslatableString(getKey(@"more_icons"), @"more icons");

        /// <summary>
        /// "hidden icons"
        /// </summary>
        public static LocalisableString HiddenIconsHeader => new TranslatableString(getKey(@"hidden_icons_header"), @"hidden icons");

        /// <summary>
        /// "Manage toolbar icons..."
        /// </summary>
        public static LocalisableString ManageToolbarIcons => new TranslatableString(getKey(@"manage_toolbar_icons"), @"Manage toolbar icons...");

        /// <summary>
        /// "Choose which icons are shown in the toolbar, their order, and hiding behaviour."
        /// </summary>
        public static LocalisableString ManageToolbarIconsDescription => new TranslatableString(getKey(@"manage_toolbar_icons_description"), @"Choose which icons are shown in the toolbar, their order, and hiding behaviour.");

        /// <summary>
        /// "Hide this icon"
        /// </summary>
        public static LocalisableString HideIcon => new TranslatableString(getKey(@"hide_icon"), @"Hide this icon");

        /// <summary>
        /// "Show this icon"
        /// </summary>
        public static LocalisableString ShowIcon => new TranslatableString(getKey(@"show_icon"), @"Show this icon");

        /// <summary>
        /// "Auto-hide icons when space is limited"
        /// </summary>
        public static LocalisableString AutoHideIcons => new TranslatableString(getKey(@"auto_hide_icons"), @"Auto-hide icons when space is limited");

        /// <summary>
        /// "Hides toolbar icons from the left as space runs out, keeping the ruleset selector visible."
        /// </summary>
        public static LocalisableString AutoHideIconsDescription => new TranslatableString(getKey(@"auto_hide_icons_description"), @"Hides toolbar icons from the left as space runs out, keeping the ruleset selector visible.");

        /// <summary>
        /// "Restore default order"
        /// </summary>
        public static LocalisableString RestoreDefaultOrder => new TranslatableString(getKey(@"restore_default_order"), @"Restore default order");

        /// <summary>
        /// "Restore default settings"
        /// </summary>
        public static LocalisableString RestoreDefaultSettings => new TranslatableString(getKey(@"restore_default_settings"), @"Restore default settings");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

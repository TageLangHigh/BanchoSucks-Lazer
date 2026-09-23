using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Utils
{
    public static class LocalisationUtils
    {
        /// <summary>
        /// Represents a localized label of two states, "on" and "off".
        /// </summary>
        public static LocalisableString BooleanStateLabel(bool state)
            => state ? CommonModsStrings.OnStateLabel : CommonModsStrings.OffStateLabel;
    }
}

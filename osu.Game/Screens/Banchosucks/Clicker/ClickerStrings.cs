// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using osu.Framework.Localisation;

namespace osu.Game.Screens.Banchosucks.Clicker
{
    /// <summary>
    /// Banchosucks: English and German texts of the osu! Clicker, chosen by the client language.
    /// </summary>
    /// <remarks>
    /// osu!'s own strings come from .resx files in the resources package, which we do not ship; the clicker keeps
    /// both languages in code instead. <see cref="Text(string, string, object[])"/> returns a <see cref="LocalisableString"/> that re-resolves
    /// when the player switches the language, for labels that live long. Texts rebuilt every frame use
    /// <see cref="IsGerman"/> and <see cref="Pick(string, string)"/> directly, which is cheaper than allocating localisable strings.
    /// </remarks>
    public static class ClickerStrings
    {
        /// <summary>
        /// Whether the client currently runs in German; set by <see cref="ClickerEngine"/> from the game language.
        /// </summary>
        public static bool IsGerman { get; set; }

        public static string Pick(string en, string de) => IsGerman ? de : en;

        public static string Pick(LocalisedName name) => name.Get(IsGerman);

        public static LocalisableString Text(string en, string de, params object[] args) => new LocalisableString(new ClickerText(en, de, args));

        public static LocalisableString Text(LocalisedName name) => new LocalisableString(new ClickerText(name.En, name.De, Array.Empty<object>()));

        /// <summary>
        /// One text in both languages. Format arguments are formatted with the language's culture (decimal comma in German).
        /// </summary>
        private sealed class ClickerText : IEquatable<ClickerText>, ILocalisableStringData
        {
            private readonly string en;
            private readonly string de;
            private readonly object[] args;

            public ClickerText(string en, string de, object[] args)
            {
                this.en = en;
                this.de = de;
                this.args = args;
            }

            public string GetLocalised(LocalisationParameters parameters)
            {
                bool german = parameters.Store?.EffectiveCulture.TwoLetterISOLanguageName == "de";
                string template = german && !string.IsNullOrEmpty(de) ? de : en;
                return args.Length == 0 ? template : string.Format(german ? ClickerFormat.GermanCulture : ClickerFormat.EnglishCulture, template, args);
            }

            public bool Equals(ClickerText? other) => other != null && en == other.en && de == other.de && args.SequenceEqual(other.args);

            public bool Equals(ILocalisableStringData? other) => other is ClickerText text && Equals(text);

            public override bool Equals(object? obj) => obj is ClickerText text && Equals(text);

            public override int GetHashCode() => HashCode.Combine(en, de, args.Length);

            public override string ToString() => GetLocalised(LocalisationParameters.DEFAULT);
        }
    }

    public static class ClickerFormat
    {
        public static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
        public static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

        private static readonly string[] german_units = { "Tsd", "Mio", "Mrd", "Bio", "Brd", "Trio", "Trd", "Quad" };
        private static readonly string[] english_units = { "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp" };

        /// <summary>
        /// Short number in the current language: "12.345" / "1,23 Mio" in German, "12,345" / "1.23M" in English.
        /// </summary>
        public static string Number(double value) => Number(value, ClickerStrings.IsGerman);

        public static string Number(double value, bool german)
        {
            var culture = german ? GermanCulture : EnglishCulture;

            if (double.IsNaN(value) || double.IsInfinity(value))
                return "?";
            if (value < 0)
                return "-" + Number(-value, german);
            if (value < 10)
                return value.ToString("0.#", culture);
            if (value < 1_000_000)
                return Math.Floor(value).ToString("N0", culture);

            string[] units = german ? german_units : english_units;
            int unit = 0;
            value /= 1000;

            while (value >= 1000 && unit < units.Length - 1)
            {
                value /= 1000;
                unit++;
            }

            string number = value.ToString("0.##", culture);
            return german ? $"{number} {units[unit]}" : $"{number}{units[unit]}";
        }

        /// <summary>
        /// Whole number with thousands separators of the current language.
        /// </summary>
        public static string Count(long value) => value.ToString("N0", ClickerStrings.IsGerman ? GermanCulture : EnglishCulture);

        /// <summary>
        /// "0:42", "12:05" or "2 h 05 min".
        /// </summary>
        public static string Duration(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds))
                seconds = 0;
            if (double.IsInfinity(seconds) || seconds > 100 * 24 * 3600)
                return "∞";

            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1)
                return ClickerStrings.IsGerman ? $"{(int)span.TotalHours} h {span.Minutes:00} min" : $"{(int)span.TotalHours}h {span.Minutes:00}m";

            return $"{span.Minutes}:{span.Seconds:00}";
        }

        public static string Percent(double factor) => (factor * 100).ToString("0.#", ClickerStrings.IsGerman ? GermanCulture : EnglishCulture) + " %";

        public static string Multiplier(double factor) => "x" + factor.ToString("0.##", ClickerStrings.IsGerman ? GermanCulture : EnglishCulture);
    }
}

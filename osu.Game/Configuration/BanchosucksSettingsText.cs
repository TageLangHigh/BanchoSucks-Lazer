// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Extensions;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: a config manager whose settings can be read and written as text by name, for the settings
    /// export / import. Implemented by <see cref="OsuConfigManager"/> and the rulesets' config managers.
    /// </summary>
    public interface IBanchosucksTextSettings
    {
        /// <summary>
        /// The current value as the config file would store it; null when there is no such setting.
        /// </summary>
        string? BanchosucksGetText(string key);

        /// <summary>
        /// Sets a value from text. Returns false, changing nothing, when the setting does not exist or the text is
        /// not a valid value for it.
        /// </summary>
        bool BanchosucksTrySetText(string key, string text);
    }

    /// <summary>
    /// Banchosucks: reads and writes bindables as text, strictly. A config file loads whatever parses; a value from
    /// an imported file has to be a real value of the setting, or the game could start with a broken setting.
    /// </summary>
    public static class BanchosucksSettingsText
    {
        public static string Get(IBindable bindable) => bindable.ToString(CultureInfo.InvariantCulture);

        public static bool TrySet(IBindable bindable, string? text)
        {
            if (text == null || bindable is not IParseable parseable || bindable.Disabled)
                return false;

            var valueType = findValueType(bindable.GetType());

            if (valueType == null)
                return false;

            valueType = Nullable.GetUnderlyingType(valueType) ?? valueType;

            if (valueType.IsEnum)
            {
                // Enum.Parse would also take "17" and produce a value no dropdown knows
                if (text.Length == 0 || char.IsDigit(text[0]) || text[0] == '-' || text[0] == '+')
                    return false;

                if (!Enum.TryParse(valueType, text, false, out object? parsed) || parsed == null)
                    return false;

                // without [Flags] a comma list is not a value either
                if (!valueType.IsDefined(typeof(FlagsAttribute), false) && (text.Contains(',') || !Enum.IsDefined(valueType, parsed)))
                    return false;
            }
            else if (valueType == typeof(float) || valueType == typeof(double))
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number))
                    return false;
            }
            else if (valueType == typeof(bool))
            {
                if (!bool.TryParse(text, out _))
                    return false;
            }
            else if (valueType == typeof(int) || valueType == typeof(long))
            {
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    return false;
            }
            else if (valueType != typeof(string))
            {
                // dates, sizes and the like are not something an import needs to set
                return false;
            }

            try
            {
                parseable.Parse(text, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Type? findValueType(Type? type)
        {
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Bindable<>))
                    return type.GetGenericArguments()[0];

                type = type.BaseType;
            }

            return null;
        }
    }
}

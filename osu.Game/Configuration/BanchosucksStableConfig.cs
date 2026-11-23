// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Game.Skinning;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: reads the settings of an osu!stable installation ("osu!.&lt;Windows user&gt;.cfg") and maps them
    /// onto lazer settings, for "Stable-Einstellungen übernehmen" (F44). Pure text in, a
    /// <see cref="BanchosucksSettingsDocument"/> out; the stable folder is only ever read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file holds lines of the form <c>Key = Value</c>. It also holds the stable account (<c>Username</c>,
    /// <c>Password</c>) and the first comment line names the user: <see cref="Parse"/> keeps the keys listed in
    /// <see cref="ReadKeys"/> and nothing else, so none of that ever gets past the parser.
    /// </para>
    /// <para>How each stable key maps:</para>
    /// <list type="table">
    /// <item><term><c>Offset</c></term><description>
    /// Universal offset in whole milliseconds (stable allows -300 to 300). In stable a positive value makes the
    /// hit objects come later relative to the music (what players with delayed audio set). Lazer's
    /// <see cref="OsuSetting.AudioOffset"/> is also in milliseconds but the other way round: a higher value makes
    /// gameplay happen earlier relative to the track. So lazer = -stable, same unit, clamped by lazer to -500..500.
    /// Both clients put their own fixed platform correction on top, so the result is the right starting point,
    /// not a guarantee; the offset wizard in the audio settings fine-tunes it.
    /// </description></item>
    /// <item><term><c>VolumeUniversal</c>, <c>VolumeEffect</c>, <c>VolumeMusic</c></term><description>
    /// Whole percent, 0 to 100. Lazer keeps the same three volumes as fractions 0 to 1
    /// (framework settings of the same names): lazer = stable / 100.
    /// </description></item>
    /// <item><term><c>MouseSpeed</c></term><description>
    /// Sensitivity multiplier, 0.4 to 6. Lazer's mouse sensitivity is the same multiplier (0.1 to 10), taken 1:1.
    /// Lazer only applies it while raw input ("high precision mouse") is on; stable also applies it without.
    /// </description></item>
    /// <item><term><c>RawInput</c></term><description>
    /// 0 or 1. Lazer's "high precision mouse" (relative mode of the mouse handler), taken 1:1.
    /// </description></item>
    /// <item><term><c>CursorSize</c></term><description>
    /// Multiplier of the skin's cursor, 0.5 to 2. <see cref="OsuSetting.GameplayCursorSize"/> is the same multiplier
    /// (0.1 to 2), taken 1:1. <c>AutomaticCursorSizing</c> (0 or 1) becomes <see cref="OsuSetting.AutoCursorSize"/>.
    /// </description></item>
    /// <item><term><c>DimLevel</c></term><description>
    /// Background dim in whole percent, 0 to 100. <see cref="OsuSetting.DimLevel"/> is a fraction: lazer = stable / 100.
    /// </description></item>
    /// <item><term><c>Skin</c></term><description>
    /// Name of the skin's folder in stable's Skins directory. Selected only if a skin of that name is already
    /// imported (the importer names a skin after its skin.ini and appends the folder in brackets when they differ).
    /// "Default" is stable's built-in skin, which is lazer's built-in classic skin.
    /// </description></item>
    /// <item><term><c>keyOsuLeft</c>, <c>keyOsuRight</c>, <c>keyOsuSmoke</c></term><description>
    /// osu! actions LeftButton (0), RightButton (1), Smoke (2).
    /// </description></item>
    /// <item><term><c>keyTaikoOuterLeft</c>, <c>keyTaikoInnerLeft</c>, <c>keyTaikoInnerRight</c>, <c>keyTaikoOuterRight</c></term><description>
    /// osu!taiko actions LeftRim (0), LeftCentre (1), RightCentre (2), RightRim (3).
    /// </description></item>
    /// <item><term><c>keyFruitsLeft</c>, <c>keyFruitsRight</c>, <c>keyFruitsDash</c></term><description>
    /// osu!catch actions MoveLeft (0), MoveRight (1), Dash (2).
    /// </description></item>
    /// <item><term><c>ManiaLayouts4K</c> ... (<c>ManiaLayouts&lt;n&gt;K</c>)</term><description>
    /// The keys of an n-key stage from left to right, separated by spaces: osu!mania variant n, actions Key1 (0)
    /// to Key&lt;n&gt; (n - 1). Only plain single-stage layouts are read; a line with another number of keys is skipped.
    /// </description></item>
    /// </list>
    /// <para>
    /// Stable has one key per action (the mouse buttons are fixed), lazer has two slots. The stable key goes into
    /// the first slot and the second slot stays as it is (for osu! that is the mouse button), unless it holds a key
    /// that now belongs to another action of the same mode: then it is emptied, like the game's own conflict handling.
    /// Key names are those of XNA's <c>Keys</c> enum (<see cref="MapKey"/>).
    /// </para>
    /// </remarks>
    public class BanchosucksStableConfig
    {
        public const string OFFSET = "Offset";
        public const string VOLUME_UNIVERSAL = "VolumeUniversal";
        public const string VOLUME_EFFECT = "VolumeEffect";
        public const string VOLUME_MUSIC = "VolumeMusic";
        public const string MOUSE_SPEED = "MouseSpeed";
        public const string RAW_INPUT = "RawInput";
        public const string CURSOR_SIZE = "CursorSize";
        public const string AUTOMATIC_CURSOR_SIZING = "AutomaticCursorSizing";
        public const string DIM_LEVEL = "DimLevel";
        public const string SKIN = "Skin";

        public const string MANIA_LAYOUT_PREFIX = "ManiaLayouts";

        /// <summary>
        /// Stable's name for its built-in skin.
        /// </summary>
        public const string DEFAULT_SKIN = "Default";

        /// <summary>
        /// The largest osu!mania stage that has key bindings in lazer.
        /// </summary>
        public const int MAX_MANIA_KEYS = 10;

        /// <summary>
        /// Stable key binding settings and the lazer action they belong to (ruleset short name, action number).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (string Ruleset, int Action)> KeyActions = new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyOsuLeft"] = ("osu", 0),
            ["keyOsuRight"] = ("osu", 1),
            ["keyOsuSmoke"] = ("osu", 2),
            ["keyTaikoOuterLeft"] = ("taiko", 0),
            ["keyTaikoInnerLeft"] = ("taiko", 1),
            ["keyTaikoInnerRight"] = ("taiko", 2),
            ["keyTaikoOuterRight"] = ("taiko", 3),
            ["keyFruitsLeft"] = ("fruits", 0),
            ["keyFruitsRight"] = ("fruits", 1),
            ["keyFruitsDash"] = ("fruits", 2),
        };

        private static readonly string[] plain_keys =
        {
            OFFSET, VOLUME_UNIVERSAL, VOLUME_EFFECT, VOLUME_MUSIC, MOUSE_SPEED, RAW_INPUT, CURSOR_SIZE, AUTOMATIC_CURSOR_SIZING, DIM_LEVEL, SKIN,
        };

        /// <summary>
        /// Every key <see cref="Parse"/> keeps. Anything else in the file is dropped unread.
        /// </summary>
        public static readonly IReadOnlyCollection<string> ReadKeys = new HashSet<string>(
            plain_keys.Concat(KeyActions.Keys).Concat(Enumerable.Range(1, MAX_MANIA_KEYS).Select(maniaLayoutKey)), StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The values that were read, by stable key.
        /// </summary>
        public IReadOnlyDictionary<string, string> Values => values;

        private static string maniaLayoutKey(int keys) => $"{MANIA_LAYOUT_PREFIX}{keys.ToString(CultureInfo.InvariantCulture)}K";

        #region Reading the file

        /// <summary>
        /// Reads the text of an osu!stable config file, keeping only <see cref="ReadKeys"/>.
        /// </summary>
        public static BanchosucksStableConfig Parse(string? text)
        {
            var config = new BanchosucksStableConfig();

            if (string.IsNullOrEmpty(text))
                return config;

            using (var reader = new StringReader(text))
            {
                while (reader.ReadLine() is string line)
                {
                    line = line.Trim();

                    if (line.Length == 0 || line[0] == '#')
                        continue;

                    int separator = line.IndexOf('=');

                    if (separator <= 0)
                        continue;

                    string key = line.Substring(0, separator).Trim();

                    if (!ReadKeys.Contains(key))
                        continue;

                    config.values[key] = line.Substring(separator + 1).Trim();
                }
            }

            return config;
        }

        /// <summary>
        /// Picks the config file of the current Windows user among the files of a stable folder, the way stable
        /// names it ("osu!.&lt;user&gt;.cfg"). If there is no file for this user but exactly one user config at all,
        /// that one is used (the folder was copied from another PC or account).
        /// </summary>
        /// <returns>The file name, or null when there is none or the choice would be a guess.</returns>
        public static string? FindConfigFile(IEnumerable<string> fileNames, string userName)
        {
            var candidates = fileNames.Select(Path.GetFileName)
                                      .Where(f => f != null && f.StartsWith("osu!.", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)
                                                  // "osu!.cfg" is the global file without any of these settings
                                                  && f.Length > "osu!..cfg".Length)
                                      .Select(f => f!)
                                      .ToList();

            string wanted = $"osu!.{userName}.cfg";

            return candidates.FirstOrDefault(f => string.Equals(f, wanted, StringComparison.Ordinal))
                   ?? candidates.FirstOrDefault(f => string.Equals(f, wanted, StringComparison.OrdinalIgnoreCase))
                   ?? (candidates.Count == 1 ? candidates[0] : null);
        }

        #endregion

        #region Mapping

        /// <summary>
        /// Builds the lazer settings this stable config stands for.
        /// </summary>
        /// <param name="current">The current lazer setup: the stable key replaces the first slot of an action, the rest of the bindings comes from here.</param>
        /// <param name="notes">Stable values that could not be used.</param>
        /// <returns>A document holding only what the stable config has a value for.</returns>
        public BanchosucksSettingsDocument Map(BanchosucksSettingsDocument current, out List<BanchosucksTransferNote> notes)
        {
            var document = new BanchosucksSettingsDocument();
            var unusable = new List<BanchosucksTransferNote>();

            void number(string stableKey, IDictionary<string, string> target, string lazerKey, double min, double max, Func<double, double> convert)
            {
                if (!values.TryGetValue(stableKey, out string? text))
                    return;

                if (TryParseNumber(text, out double value) && value >= min && value <= max)
                    target[lazerKey] = convert(value).ToString(CultureInfo.InvariantCulture);
                else
                    unusable.Add(new BanchosucksTransferNote(BanchosucksTransferNoteType.StableValueUnusable, stableKey));
            }

            void flag(string stableKey, IDictionary<string, string> target, string lazerKey)
            {
                if (!values.TryGetValue(stableKey, out string? text))
                    return;

                if (TryParseFlag(text, out bool value))
                    target[lazerKey] = value.ToString(CultureInfo.InvariantCulture);
                else
                    unusable.Add(new BanchosucksTransferNote(BanchosucksTransferNoteType.StableValueUnusable, stableKey));
            }

            // milliseconds in both, opposite sign (see the class remarks)
            number(OFFSET, document.Game, nameof(OsuSetting.AudioOffset), -500, 500, ConvertOffset);

            // percent to fraction
            number(VOLUME_UNIVERSAL, document.Framework, "VolumeUniversal", 0, 100, ConvertPercent);
            number(VOLUME_EFFECT, document.Framework, "VolumeEffect", 0, 100, ConvertPercent);
            number(VOLUME_MUSIC, document.Framework, "VolumeMusic", 0, 100, ConvertPercent);
            number(DIM_LEVEL, document.Game, nameof(OsuSetting.DimLevel), 0, 100, ConvertPercent);

            // same multipliers in both
            number(MOUSE_SPEED, document.Mouse, BanchosucksSettingsAllowList.MOUSE_SENSITIVITY, 0.1, 10, v => v);
            number(CURSOR_SIZE, document.Game, nameof(OsuSetting.GameplayCursorSize), 0.1, 2, v => v);

            flag(RAW_INPUT, document.Mouse, BanchosucksSettingsAllowList.MOUSE_RELATIVE_MODE);
            flag(AUTOMATIC_CURSOR_SIZING, document.Game, nameof(OsuSetting.AutoCursorSize));

            if (values.TryGetValue(SKIN, out string? skin) && skin.Length > 0)
            {
                document.Skin = string.Equals(skin, DEFAULT_SKIN, StringComparison.OrdinalIgnoreCase)
                    ? new BanchosucksSkinReference { Name = skin, ID = SkinInfo.CLASSIC_SKIN }
                    : new BanchosucksSkinReference { Name = skin };
            }

            mapKeys(current, document, unusable);

            notes = unusable;
            return document;
        }

        private void mapKeys(BanchosucksSettingsDocument current, BanchosucksSettingsDocument document, List<BanchosucksTransferNote> unusable)
        {
            // (ruleset, variant, action) -> key text of the first slot
            var wanted = new Dictionary<(string Ruleset, int Variant, int Action), string>();

            foreach (var (stableKey, (ruleset, action)) in KeyActions)
            {
                if (!values.TryGetValue(stableKey, out string? text))
                    continue;

                if (MapKey(text) is InputKey key)
                    wanted[(ruleset, 0, action)] = BanchosucksKeyBindings.ToText(new KeyCombination(key));
                else
                    unusable.Add(new BanchosucksTransferNote(BanchosucksTransferNoteType.StableValueUnusable, stableKey));
            }

            for (int keyCount = 1; keyCount <= MAX_MANIA_KEYS; keyCount++)
            {
                string stableKey = maniaLayoutKey(keyCount);

                if (!values.TryGetValue(stableKey, out string? text))
                    continue;

                var keys = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(MapKey).ToList();

                if (keys.Count != keyCount || keys.Any(k => k == null))
                {
                    unusable.Add(new BanchosucksTransferNote(BanchosucksTransferNoteType.StableValueUnusable, stableKey));
                    continue;
                }

                // single stage: the variant number is the key count, the actions are Key1.. from the left
                for (int column = 0; column < keyCount; column++)
                    wanted[("mania", keyCount, column)] = BanchosucksKeyBindings.ToText(new KeyCombination(keys[column]!.Value));
            }

            // start from the current bindings of every ruleset and variant that gets at least one key
            var touched = wanted.Keys.Select(k => (k.Ruleset, k.Variant)).ToHashSet();
            var result = current.KeyBindings.Where(e => touched.Contains((e.Ruleset, e.Variant))).Select(e => e.Clone()).ToList();

            var set = new HashSet<BanchosucksKeyBindingEntry>();

            foreach (var entry in result)
            {
                if (entry.Keys.Count > 0 && wanted.TryGetValue((entry.Ruleset, entry.Variant, entry.Action), out string? key))
                {
                    entry.Keys[0] = key;
                    set.Add(entry);
                }
            }

            // a key can only belong to one action of a mode: empty the slots that still hold a key which was just given to another action
            foreach (var entry in result)
            {
                for (int slot = 0; slot < entry.Keys.Count; slot++)
                {
                    if (slot == 0 && set.Contains(entry))
                        continue;

                    string held = entry.Keys[slot];

                    if (held != BanchosucksKeyBindings.NONE
                        && result.Any(other => other != entry && set.Contains(other) && other.Ruleset == entry.Ruleset && other.Variant == entry.Variant && other.Keys[0] == held))
                        entry.Keys[slot] = BanchosucksKeyBindings.NONE;
                }
            }

            document.KeyBindings = result;
        }

        /// <summary>
        /// Stable's universal offset to lazer's audio offset: same unit (ms), opposite sign.
        /// </summary>
        public static double ConvertOffset(double stableOffset) => stableOffset == 0 ? 0 : -stableOffset;

        /// <summary>
        /// Stable's percent values (volume, dim) to lazer's fractions.
        /// </summary>
        public static double ConvertPercent(double stablePercent) => Math.Round(stablePercent / 100, 4);

        /// <summary>
        /// Reads a number the way stable may have written it, with a point or a comma as the decimal sign.
        /// </summary>
        public static bool TryParseNumber(string? text, out double value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
                   && double.IsFinite(value);
        }

        /// <summary>
        /// Reads a yes/no value: stable writes 1 and 0, older files True and False.
        /// </summary>
        public static bool TryParseFlag(string? text, out bool value)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                    value = true;
                    return true;

                case "0":
                case "false":
                    value = false;
                    return true;

                default:
                    value = false;
                    return false;
            }
        }

        /// <summary>
        /// Names of XNA's <c>Keys</c> enum (what stable writes) that are called differently in lazer.
        /// Letters, F-keys, arrows and the keys named the same (Space, Enter, Tab, Escape, Insert, Delete, Home, End,
        /// PageUp, PageDown, CapsLock, NumLock, PrintScreen, Pause) need no entry.
        /// </summary>
        private static readonly Dictionary<string, InputKey> xna_keys = new Dictionary<string, InputKey>(StringComparer.OrdinalIgnoreCase)
        {
            ["D0"] = InputKey.Number0,
            ["D1"] = InputKey.Number1,
            ["D2"] = InputKey.Number2,
            ["D3"] = InputKey.Number3,
            ["D4"] = InputKey.Number4,
            ["D5"] = InputKey.Number5,
            ["D6"] = InputKey.Number6,
            ["D7"] = InputKey.Number7,
            ["D8"] = InputKey.Number8,
            ["D9"] = InputKey.Number9,
            ["NumPad0"] = InputKey.Keypad0,
            ["NumPad1"] = InputKey.Keypad1,
            ["NumPad2"] = InputKey.Keypad2,
            ["NumPad3"] = InputKey.Keypad3,
            ["NumPad4"] = InputKey.Keypad4,
            ["NumPad5"] = InputKey.Keypad5,
            ["NumPad6"] = InputKey.Keypad6,
            ["NumPad7"] = InputKey.Keypad7,
            ["NumPad8"] = InputKey.Keypad8,
            ["NumPad9"] = InputKey.Keypad9,
            ["Multiply"] = InputKey.KeypadMultiply,
            ["Add"] = InputKey.KeypadPlus,
            ["Subtract"] = InputKey.KeypadMinus,
            ["Divide"] = InputKey.KeypadDivide,
            ["Decimal"] = InputKey.KeypadPeriod,
            ["Back"] = InputKey.BackSpace,
            ["Scroll"] = InputKey.ScrollLock,
            ["LeftShift"] = InputKey.LShift,
            ["RightShift"] = InputKey.RShift,
            ["LeftControl"] = InputKey.LControl,
            ["RightControl"] = InputKey.RControl,
            ["LeftAlt"] = InputKey.LAlt,
            ["RightAlt"] = InputKey.RAlt,
            ["LeftWindows"] = InputKey.LSuper,
            ["RightWindows"] = InputKey.RSuper,
            ["Apps"] = InputKey.Menu,
            ["OemTilde"] = InputKey.Tilde,
            ["OemMinus"] = InputKey.Minus,
            ["OemPlus"] = InputKey.Plus,
            ["OemOpenBrackets"] = InputKey.BracketLeft,
            ["OemCloseBrackets"] = InputKey.BracketRight,
            ["OemPipe"] = InputKey.BackSlash,
            ["OemSemicolon"] = InputKey.Semicolon,
            ["OemQuotes"] = InputKey.Quote,
            ["OemComma"] = InputKey.Comma,
            ["OemPeriod"] = InputKey.Period,
            ["OemQuestion"] = InputKey.Slash,
            ["OemBackslash"] = InputKey.NonUSBackSlash,
        };

        /// <summary>
        /// Keys that are named the same in XNA and in lazer and may be bound to gameplay.
        /// </summary>
        private static readonly HashSet<InputKey> same_named_keys = new HashSet<InputKey>(
            Enumerable.Range((int)InputKey.A, InputKey.Z - InputKey.A + 1).Select(k => (InputKey)k)
                      .Concat(Enumerable.Range((int)InputKey.F1, InputKey.F24 - InputKey.F1 + 1).Select(k => (InputKey)k))
                      .Concat(new[]
                      {
                          InputKey.Up, InputKey.Down, InputKey.Left, InputKey.Right, InputKey.Space, InputKey.Enter, InputKey.Tab, InputKey.Escape,
                          InputKey.Insert, InputKey.Delete, InputKey.Home, InputKey.End, InputKey.PageUp, InputKey.PageDown,
                          InputKey.CapsLock, InputKey.NumLock, InputKey.PrintScreen, InputKey.Pause,
                      }));

        /// <summary>
        /// The lazer key for a key name from a stable config; null for "None" and for names without a counterpart.
        /// </summary>
        public static InputKey? MapKey(string? stableName)
        {
            if (string.IsNullOrWhiteSpace(stableName))
                return null;

            stableName = stableName.Trim();

            if (xna_keys.TryGetValue(stableName, out var mapped))
                return mapped;

            // only real names: Enum.TryParse would also take numbers and comma lists
            if (!stableName.All(char.IsLetterOrDigit) || char.IsDigit(stableName[0]))
                return null;

            if (Enum.TryParse(stableName, true, out InputKey key) && same_named_keys.Contains(key))
                return key;

            return null;
        }

        #endregion

        #region Preview texts

        /// <summary>
        /// English and German title of a single change in the preview; null for changes that are shown grouped (keys).
        /// </summary>
        public static (string En, string De)? TitleOf(BanchosucksSettingsChange change)
        {
            switch (change.Section)
            {
                case BanchosucksSettingsSection.Game:
                    switch (change.Key)
                    {
                        case nameof(OsuSetting.AudioOffset):
                            return ("Universal audio offset in ms (lazer counts the other way round)", "Universeller Audio-Offset in ms (Lazer zählt andersherum)");

                        case nameof(OsuSetting.DimLevel):
                            return ("Background dim", "Hintergrund-Abdunklung");

                        case nameof(OsuSetting.GameplayCursorSize):
                            return ("Cursor size", "Cursorgröße");

                        case nameof(OsuSetting.AutoCursorSize):
                            return ("Automatic cursor size", "Automatische Cursorgröße");
                    }

                    break;

                case BanchosucksSettingsSection.Framework:
                    switch (change.Key)
                    {
                        case "VolumeUniversal":
                            return ("Master volume", "Gesamtlautstärke");

                        case "VolumeEffect":
                            return ("Effect volume", "Effektlautstärke");

                        case "VolumeMusic":
                            return ("Music volume", "Musiklautstärke");
                    }

                    break;

                case BanchosucksSettingsSection.Mouse:
                    return change.Key == BanchosucksSettingsAllowList.MOUSE_SENSITIVITY
                        ? ("Mouse sensitivity", "Mausempfindlichkeit")
                        : ("Raw input (high precision mouse)", "Raw Input (Hochpräzisionsmaus)");

                case BanchosucksSettingsSection.Skin:
                    return ("Skin", "Skin");
            }

            return null;
        }

        #endregion
    }
}

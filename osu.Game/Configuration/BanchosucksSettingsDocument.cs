// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: the selected skin in a settings export. Only the name travels (plus the ID, which matches on the
    /// same installation and for the built-in skins); the skin itself is not part of the file.
    /// </summary>
    public class BanchosucksSkinReference
    {
        public string Name = string.Empty;
        public Guid? ID;
    }

    /// <summary>
    /// Banchosucks: one per-ruleset setting (the database's ruleset settings) in a settings export.
    /// </summary>
    public class BanchosucksRulesetSettingEntry
    {
        public string Ruleset = string.Empty;
        public int Variant;
        public string Key = string.Empty;
        public string Value = string.Empty;
    }

    /// <summary>
    /// Banchosucks: the content of a settings export ("Einstellungen sichern"), one JSON file:
    /// <code>
    /// {
    ///   "format": "banchosucks-settings",
    ///   "version": 1,
    ///   "client": "2026.913.17",
    ///   "created": "2026-11-23T18:00:00Z",
    ///   "game": { "DimLevel": "0.7", ... },              // OsuSetting name -> text as in osu.cfg
    ///   "framework": { "FrameSync": "Limit2x", ... },    // FrameworkSetting name -> text as in framework.ini
    ///   "mouse": { "Sensitivity": "1.5", "UseRelativeMode": "True" },
    ///   "skin": { "name": "My Skin", "id": "..." },
    ///   "keyBindings": [ { "ruleset": "osu", "variant": 0, "action": 0, "keys": ["Z", "MouseLeft"] }, ... ],
    ///   "rulesetSettings": [ { "ruleset": "mania", "variant": 0, "key": "ScrollSpeed", "value": "200" }, ... ],
    ///   "keyProfiles": [ { "name": "...", "ruleset": "mania", "variant": 4, "bindings": [ ... ] } ]
    /// }
    /// </code>
    /// Reading is forgiving: sections and entries this client does not know (a newer version, a ruleset or setting
    /// that does not exist here, anything not on <see cref="BanchosucksSettingsAllowList"/>) are skipped and listed
    /// in <see cref="Skipped"/>, never an error. Only a text that is not such a file at all is refused.
    /// </summary>
    public class BanchosucksSettingsDocument
    {
        public const string FORMAT = "banchosucks-settings";
        public const int VERSION = 1;
        public const string FILE_EXTENSION = ".json";

        public int Version = VERSION;

        /// <summary>
        /// The client version that wrote the file; information only.
        /// </summary>
        public string Client = string.Empty;

        public DateTime? Created;

        public readonly SortedDictionary<string, string> Game = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, string> Framework = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, string> Mouse = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public BanchosucksSkinReference? Skin;

        public List<BanchosucksKeyBindingEntry> KeyBindings = new List<BanchosucksKeyBindingEntry>();
        public List<BanchosucksRulesetSettingEntry> RulesetSettings = new List<BanchosucksRulesetSettingEntry>();
        public List<BanchosucksKeyProfile> KeyProfiles = new List<BanchosucksKeyProfile>();

        /// <summary>
        /// What was left out while reading the file, for the log ("game: FancyNewSetting").
        /// </summary>
        public readonly List<string> Skipped = new List<string>();

        /// <summary>
        /// Whether the file was written by a client with a newer format; what this client understands is still used.
        /// </summary>
        public bool IsNewerVersion => Version > VERSION;

        public bool IsEmpty => Game.Count == 0 && Framework.Count == 0 && Mouse.Count == 0 && Skin == null
                               && KeyBindings.Count == 0 && RulesetSettings.Count == 0 && KeyProfiles.Count == 0;

        public string Serialise()
        {
            var root = new JObject
            {
                ["format"] = FORMAT,
                ["version"] = VERSION,
            };

            if (!string.IsNullOrEmpty(Client))
                root["client"] = Client;

            if (Created is DateTime created)
                root["created"] = created.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

            // the allow-list is applied again on the way out, whatever the caller put into the document
            root["game"] = toJson(Game.Where(p => BanchosucksSettingsAllowList.IsGameSettingAllowed(p.Key)));
            root["framework"] = toJson(Framework.Where(p => BanchosucksSettingsAllowList.IsFrameworkSettingAllowed(p.Key)));
            root["mouse"] = toJson(Mouse.Where(p => BanchosucksSettingsAllowList.IsMouseSettingAllowed(p.Key)));

            if (Skin != null && !string.IsNullOrEmpty(Skin.Name))
            {
                var skin = new JObject { ["name"] = Skin.Name };
                if (Skin.ID is Guid id)
                    skin["id"] = id.ToString();
                root["skin"] = skin;
            }

            root["keyBindings"] = BanchosucksKeyProfiles.BindingsToJson(KeyBindings);

            var rulesetSettings = new JArray();

            foreach (var setting in RulesetSettings.Where(s => BanchosucksSettingsAllowList.IsRulesetSettingAllowed(s.Ruleset, s.Key)))
            {
                rulesetSettings.Add(new JObject
                {
                    ["ruleset"] = setting.Ruleset,
                    ["variant"] = setting.Variant,
                    ["key"] = setting.Key,
                    ["value"] = setting.Value,
                });
            }

            root["rulesetSettings"] = rulesetSettings;
            root["keyProfiles"] = BanchosucksKeyProfiles.ToJson(KeyProfiles);

            return root.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Reads an export.
        /// </summary>
        /// <exception cref="FormatException">The text is not a settings export.</exception>
        public static BanchosucksSettingsDocument Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("The file is empty.");

            JToken token;

            try
            {
                token = BanchosucksJson.Parse(json);
            }
            catch (JsonException e)
            {
                throw new FormatException("The file is not JSON.", e);
            }

            if (token is not JObject root || BanchosucksJson.StringOf(root["format"]) != FORMAT)
                throw new FormatException("The file is not a settings export.");

            var document = new BanchosucksSettingsDocument
            {
                Version = BanchosucksJson.IntOf(root["version"]) ?? VERSION,
                Client = BanchosucksJson.StringOf(root["client"]) ?? string.Empty,
            };

            if (document.Version < 1)
                throw new FormatException("The file has no usable version.");

            if (BanchosucksJson.StringOf(root["created"]) is string createdText
                && DateTime.TryParse(createdText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var created))
                document.Created = created;

            foreach (var property in root.Properties())
            {
                switch (property.Name)
                {
                    case "format":
                    case "version":
                    case "client":
                    case "created":
                        break;

                    case "game":
                        readValues(property.Value, document.Game, BanchosucksSettingsAllowList.IsGameSettingAllowed, "game", document.Skipped);
                        break;

                    case "framework":
                        readValues(property.Value, document.Framework, BanchosucksSettingsAllowList.IsFrameworkSettingAllowed, "framework", document.Skipped);
                        break;

                    case "mouse":
                        readValues(property.Value, document.Mouse, BanchosucksSettingsAllowList.IsMouseSettingAllowed, "mouse", document.Skipped);
                        break;

                    case "skin":
                        if (property.Value is JObject skin && BanchosucksJson.StringOf(skin["name"]) is string skinName && skinName.Length > 0)
                        {
                            document.Skin = new BanchosucksSkinReference
                            {
                                Name = skinName,
                                ID = Guid.TryParse(BanchosucksJson.StringOf(skin["id"]), out var id) ? id : null,
                            };
                        }
                        else
                            document.Skipped.Add("skin");

                        break;

                    case "keyBindings":
                        document.KeyBindings = BanchosucksKeyProfiles.BindingsFromJson(property.Value);
                        noteDropped(property.Value, document.KeyBindings.Count, "keyBindings", document.Skipped);
                        break;

                    case "rulesetSettings":
                        readRulesetSettings(property.Value, document);
                        break;

                    case "keyProfiles":
                        document.KeyProfiles = BanchosucksKeyProfiles.FromJson(property.Value);
                        noteDropped(property.Value, document.KeyProfiles.Count, "keyProfiles", document.Skipped);
                        break;

                    default:
                        // a section from a newer client
                        document.Skipped.Add(property.Name);
                        break;
                }
            }

            return document;
        }

        private static JObject toJson(IEnumerable<KeyValuePair<string, string>> values)
        {
            var result = new JObject();

            foreach (var pair in values)
                result[pair.Key] = pair.Value;

            return result;
        }

        private static void readValues(JToken token, IDictionary<string, string> target, Func<string, bool> allowed, string section, List<string> skipped)
        {
            if (token is not JObject values)
            {
                skipped.Add(section);
                return;
            }

            foreach (var property in values.Properties())
            {
                // numbers and booleans are accepted too, in case somebody edited the file by hand
                string? value = property.Value is JValue { Type: JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Boolean } v
                    ? Convert.ToString(v.Value, CultureInfo.InvariantCulture)
                    : null;

                if (value == null || !allowed(property.Name))
                {
                    skipped.Add($"{section}: {property.Name}");
                    continue;
                }

                target[property.Name] = value;
            }
        }

        private static void readRulesetSettings(JToken token, BanchosucksSettingsDocument document)
        {
            if (token is not JArray array)
            {
                document.Skipped.Add("rulesetSettings");
                return;
            }

            foreach (var item in array)
            {
                if (item is JObject entry
                    && BanchosucksJson.StringOf(entry["ruleset"]) is string ruleset
                    && BanchosucksJson.StringOf(entry["key"]) is string key
                    && BanchosucksJson.StringOf(entry["value"]) is string value
                    && BanchosucksSettingsAllowList.IsRulesetSettingAllowed(ruleset, key))
                {
                    document.RulesetSettings.Add(new BanchosucksRulesetSettingEntry
                    {
                        Ruleset = ruleset,
                        Variant = BanchosucksJson.IntOf(entry["variant"]) ?? 0,
                        Key = key,
                        Value = value,
                    });
                }
                else
                    document.Skipped.Add($"rulesetSettings: {(item as JObject)?["ruleset"]} {(item as JObject)?["key"]}".TrimEnd());
            }
        }

        private static void noteDropped(JToken token, int kept, string section, List<string> skipped)
        {
            int total = (token as JArray)?.Count ?? 0;

            if (token is not JArray || total > kept)
                skipped.Add($"{section}: {Math.Max(1, total - kept)}");
        }
    }

    /// <summary>
    /// Banchosucks: strict readers for JSON values that may have been edited by hand or written by a newer client.
    /// </summary>
    public static class BanchosucksJson
    {
        /// <summary>
        /// Parses JSON without turning texts that look like dates into dates (a value has to come back as written).
        /// </summary>
        public static JToken Parse(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                return JToken.Load(reader);
        }

        public static string? StringOf(JToken? token) => token is JValue { Type: JTokenType.String } value ? (string?)value.Value : null;

        public static int? IntOf(JToken? token)
        {
            if (token is JValue { Type: JTokenType.Integer, Value: long number } && number >= int.MinValue && number <= int.MaxValue)
                return (int)number;

            return null;
        }
    }
}

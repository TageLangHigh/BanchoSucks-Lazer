// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: a named set of key bindings for one ruleset and variant ("Tasten-Profil").
    /// </summary>
    public class BanchosucksKeyProfile
    {
        public string Name = string.Empty;

        /// <summary>
        /// Short name of the ruleset the profile belongs to.
        /// </summary>
        public string Ruleset = string.Empty;

        /// <summary>
        /// The key binding variant, so that osu!mania 4K and 7K keep separate profiles.
        /// </summary>
        public int Variant;

        public List<BanchosucksKeyBindingEntry> Bindings = new List<BanchosucksKeyBindingEntry>();

        public BanchosucksKeyProfile Clone() => new BanchosucksKeyProfile
        {
            Name = Name,
            Ruleset = Ruleset,
            Variant = Variant,
            Bindings = Bindings.Select(b => b.Clone()).ToList(),
        };

        /// <summary>
        /// Whether both profiles are for the same ruleset and variant and bind the same keys.
        /// </summary>
        public bool SameContentAs(BanchosucksKeyProfile other)
        {
            if (Ruleset != other.Ruleset || Variant != other.Variant || Bindings.Count != other.Bindings.Count)
                return false;

            var theirs = other.Bindings.ToDictionary(b => b.Action, b => b.Keys);
            return Bindings.All(b => theirs.TryGetValue(b.Action, out var keys) && keys.SequenceEqual(b.Keys));
        }
    }

    /// <summary>
    /// Banchosucks: the saved key profiles of this installation. A plain list with (de)serialisation; the file lives
    /// in the game data folder (<see cref="FILENAME"/>) and is also part of a settings export.
    /// </summary>
    public class BanchosucksKeyProfiles
    {
        public const string DIRECTORY = "banchosucks";
        public const string FILENAME = "key-profiles.json";

        public const int VERSION = 1;
        public const int MAX_NAME_LENGTH = 40;

        /// <summary>
        /// The profile that is written automatically before another profile replaces the bindings, so that the
        /// player can switch back. One per ruleset and variant.
        /// </summary>
        public const string BACKUP_NAME = "Sicherung / Backup";

        public readonly List<BanchosucksKeyProfile> Profiles = new List<BanchosucksKeyProfile>();

        /// <summary>
        /// The profiles of one ruleset and variant, the automatic backup last.
        /// </summary>
        public IEnumerable<BanchosucksKeyProfile> For(string ruleset, int variant)
            => Profiles.Where(p => p.Ruleset == ruleset && p.Variant == variant)
                       .OrderBy(p => p.Name == BACKUP_NAME)
                       .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase);

        public BanchosucksKeyProfile? Find(string ruleset, int variant, string name)
            => Profiles.FirstOrDefault(p => p.Ruleset == ruleset && p.Variant == variant && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Trims a name the player typed; null when nothing usable is left.
        /// </summary>
        public static string? CleanName(string? name)
        {
            if (name == null)
                return null;

            string cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

            if (cleaned.Length > MAX_NAME_LENGTH)
                cleaned = cleaned.Substring(0, MAX_NAME_LENGTH).Trim();

            return cleaned.Length == 0 ? null : cleaned;
        }

        /// <summary>
        /// Stores bindings under a name, replacing a profile of the same name for the same ruleset and variant.
        /// </summary>
        /// <returns>The stored profile, or null when the name is unusable.</returns>
        public BanchosucksKeyProfile? Save(string ruleset, int variant, string? name, IEnumerable<BanchosucksKeyBindingEntry> bindings)
        {
            string? cleaned = CleanName(name);

            if (cleaned == null)
                return null;

            var profile = new BanchosucksKeyProfile
            {
                Name = cleaned,
                Ruleset = ruleset,
                Variant = variant,
                Bindings = BanchosucksKeyBindings.Select(bindings, ruleset, variant),
            };

            Delete(ruleset, variant, cleaned);
            Profiles.Add(profile);
            return profile;
        }

        public bool Delete(string ruleset, int variant, string name)
            => Profiles.RemoveAll(p => p.Ruleset == ruleset && p.Variant == variant && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;

        /// <summary>
        /// Adds profiles from another list (a settings import); a profile of the same name is replaced.
        /// </summary>
        public void Merge(IEnumerable<BanchosucksKeyProfile> incoming)
        {
            foreach (var profile in incoming)
            {
                if (CleanName(profile.Name) is not string name)
                    continue;

                Delete(profile.Ruleset, profile.Variant, name);

                var copy = profile.Clone();
                copy.Name = name;
                Profiles.Add(copy);
            }
        }

        public JArray ToJson() => ToJson(Profiles);

        public static JArray ToJson(IEnumerable<BanchosucksKeyProfile> profiles)
        {
            var array = new JArray();

            foreach (var profile in profiles)
            {
                array.Add(new JObject
                {
                    ["name"] = profile.Name,
                    ["ruleset"] = profile.Ruleset,
                    ["variant"] = profile.Variant,
                    ["bindings"] = BindingsToJson(profile.Bindings),
                });
            }

            return array;
        }

        /// <summary>
        /// Reads profiles; entries that are not understood are left out.
        /// </summary>
        public static List<BanchosucksKeyProfile> FromJson(JToken? token)
        {
            var profiles = new List<BanchosucksKeyProfile>();

            if (token is not JArray array)
                return profiles;

            foreach (var item in array.OfType<JObject>())
            {
                string? name = CleanName(stringOf(item["name"]));
                string? ruleset = stringOf(item["ruleset"]);

                if (name == null || string.IsNullOrEmpty(ruleset) || intOf(item["variant"]) is not int variant)
                    continue;

                // a profile only ever holds bindings of its own ruleset and variant
                var bindings = BindingsFromJson(item["bindings"]).Where(b => b.Ruleset == ruleset && b.Variant == variant).ToList();

                if (bindings.Count == 0)
                    continue;

                profiles.Add(new BanchosucksKeyProfile { Name = name, Ruleset = ruleset, Variant = variant, Bindings = bindings });
            }

            return profiles;
        }

        public static JArray BindingsToJson(IEnumerable<BanchosucksKeyBindingEntry> bindings)
        {
            var array = new JArray();

            foreach (var binding in bindings)
            {
                array.Add(new JObject
                {
                    ["ruleset"] = binding.Ruleset,
                    ["variant"] = binding.Variant,
                    ["action"] = binding.Action,
                    ["keys"] = new JArray(binding.Keys.Cast<object>().ToArray()),
                });
            }

            return array;
        }

        /// <summary>
        /// Reads binding entries; an entry without a usable action or key list is left out. Key texts are kept as
        /// written, they are only checked when they are applied (<see cref="BanchosucksKeyBindings.Merge"/>).
        /// </summary>
        public static List<BanchosucksKeyBindingEntry> BindingsFromJson(JToken? token)
        {
            var bindings = new List<BanchosucksKeyBindingEntry>();

            if (token is not JArray array)
                return bindings;

            foreach (var item in array.OfType<JObject>())
            {
                if (intOf(item["action"]) is not int action || item["keys"] is not JArray keys)
                    continue;

                var texts = keys.Select(stringOf).ToList();

                if (texts.Count == 0 || texts.Any(t => t == null))
                    continue;

                bindings.Add(new BanchosucksKeyBindingEntry
                {
                    Ruleset = stringOf(item["ruleset"]) ?? string.Empty,
                    Variant = intOf(item["variant"]) ?? 0,
                    Action = action,
                    Keys = texts!,
                });
            }

            return bindings;
        }

        public string Serialise() => new JObject
        {
            ["format"] = "banchosucks-key-profiles",
            ["version"] = VERSION,
            ["profiles"] = ToJson(),
        }.ToString();

        public static BanchosucksKeyProfiles Deserialise(string? json)
        {
            var result = new BanchosucksKeyProfiles();

            if (string.IsNullOrWhiteSpace(json))
                return result;

            try
            {
                if (BanchosucksJson.Parse(json) is JObject root)
                    result.Profiles.AddRange(FromJson(root["profiles"]));
            }
            catch (Exception e)
            {
                Logger.Log($"Banchosucks key profiles could not be read: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }

            return result;
        }

        /// <summary>
        /// Reads the profiles of this installation; an empty list when there is no file yet.
        /// </summary>
        public static BanchosucksKeyProfiles Load(Storage storage)
        {
            try
            {
                var directory = storage.GetStorageForDirectory(DIRECTORY);

                if (!directory.Exists(FILENAME))
                    return new BanchosucksKeyProfiles();

                using (var stream = directory.GetStream(FILENAME))
                using (var reader = new StreamReader(stream))
                    return Deserialise(reader.ReadToEnd());
            }
            catch (Exception e)
            {
                Logger.Log($"Banchosucks key profiles could not be loaded: {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                return new BanchosucksKeyProfiles();
            }
        }

        public void Store(Storage storage)
        {
            var directory = storage.GetStorageForDirectory(DIRECTORY);

            using (var stream = directory.CreateFileSafely(FILENAME))
            using (var writer = new StreamWriter(stream))
                writer.Write(Serialise());
        }

        private static string? stringOf(JToken? token) => BanchosucksJson.StringOf(token);

        private static int? intOf(JToken? token) => BanchosucksJson.IntOf(token);
    }
}

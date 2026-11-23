// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace osu.Game.Configuration
{
    public enum BanchosucksSettingsSection
    {
        Game,
        Framework,
        Mouse,
        Skin,
        KeyBindings,
        RulesetSettings,
        KeyProfiles,
    }

    /// <summary>
    /// Banchosucks: one thing an import would change. <see cref="Key"/> is the setting name, the action number of a
    /// key binding or the name of a key profile; <see cref="Ruleset"/> and <see cref="Variant"/> are only used by
    /// key bindings, ruleset settings and key profiles.
    /// </summary>
    public record BanchosucksSettingsChange(BanchosucksSettingsSection Section, string Ruleset, int Variant, string Key, string Old, string New);

    /// <summary>
    /// Banchosucks: works out what an incoming settings document would change ("what will change" preview) and cuts
    /// a document down to the changes the player accepted. No game access in here.
    /// </summary>
    public static class BanchosucksSettingsDiff
    {
        /// <summary>
        /// Everything in <paramref name="incoming"/> that differs from <paramref name="current"/>. Entries the
        /// current setup does not have (a setting, ruleset or action this client does not know) are not changes:
        /// they could not be applied.
        /// </summary>
        public static List<BanchosucksSettingsChange> Compare(BanchosucksSettingsDocument current, BanchosucksSettingsDocument incoming)
        {
            var changes = new List<BanchosucksSettingsChange>();

            compareValues(BanchosucksSettingsSection.Game, current.Game, incoming.Game, changes);
            compareValues(BanchosucksSettingsSection.Framework, current.Framework, incoming.Framework, changes);
            compareValues(BanchosucksSettingsSection.Mouse, current.Mouse, incoming.Mouse, changes);

            if (incoming.Skin != null && !sameSkin(current.Skin, incoming.Skin))
                changes.Add(new BanchosucksSettingsChange(BanchosucksSettingsSection.Skin, string.Empty, 0, "Skin", current.Skin?.Name ?? string.Empty, incoming.Skin.Name));

            var currentBindings = toLookup(current.KeyBindings);

            foreach (var entry in incoming.KeyBindings)
            {
                if (!currentBindings.TryGetValue((entry.Ruleset, entry.Variant, entry.Action), out var existing))
                    continue;

                var merged = BanchosucksKeyBindings.Merge(existing, entry);

                if (!merged.SequenceEqual(existing.Keys))
                {
                    changes.Add(new BanchosucksSettingsChange(BanchosucksSettingsSection.KeyBindings, entry.Ruleset, entry.Variant,
                        entry.Action.ToString(CultureInfo.InvariantCulture), string.Join(", ", existing.Keys), string.Join(", ", merged)));
                }
            }

            foreach (var setting in incoming.RulesetSettings)
            {
                var existing = current.RulesetSettings.FirstOrDefault(s => s.Ruleset == setting.Ruleset && s.Variant == setting.Variant && s.Key == setting.Key);

                if (existing != null && !SameValue(existing.Value, setting.Value))
                    changes.Add(new BanchosucksSettingsChange(BanchosucksSettingsSection.RulesetSettings, setting.Ruleset, setting.Variant, setting.Key, Display(existing.Value), Display(setting.Value)));
            }

            foreach (var profile in incoming.KeyProfiles)
            {
                var existing = current.KeyProfiles.FirstOrDefault(p => p.Ruleset == profile.Ruleset && p.Variant == profile.Variant
                                                                       && string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

                if (existing == null || !existing.SameContentAs(profile))
                {
                    changes.Add(new BanchosucksSettingsChange(BanchosucksSettingsSection.KeyProfiles, profile.Ruleset, profile.Variant, profile.Name,
                        existing == null ? string.Empty : existing.Name, profile.Name));
                }
            }

            return changes;
        }

        /// <summary>
        /// A copy of <paramref name="incoming"/> that only holds what the <paramref name="accepted"/> changes refer to.
        /// </summary>
        public static BanchosucksSettingsDocument Restrict(BanchosucksSettingsDocument incoming, IEnumerable<BanchosucksSettingsChange> accepted)
        {
            var wanted = accepted.Select(c => (c.Section, c.Ruleset, c.Variant, c.Key)).ToHashSet();

            var result = new BanchosucksSettingsDocument
            {
                Version = incoming.Version,
                Client = incoming.Client,
                Created = incoming.Created,
            };

            foreach (var pair in incoming.Game.Where(p => wanted.Contains((BanchosucksSettingsSection.Game, string.Empty, 0, p.Key))))
                result.Game[pair.Key] = pair.Value;

            foreach (var pair in incoming.Framework.Where(p => wanted.Contains((BanchosucksSettingsSection.Framework, string.Empty, 0, p.Key))))
                result.Framework[pair.Key] = pair.Value;

            foreach (var pair in incoming.Mouse.Where(p => wanted.Contains((BanchosucksSettingsSection.Mouse, string.Empty, 0, p.Key))))
                result.Mouse[pair.Key] = pair.Value;

            if (incoming.Skin != null && wanted.Contains((BanchosucksSettingsSection.Skin, string.Empty, 0, "Skin")))
                result.Skin = new BanchosucksSkinReference { Name = incoming.Skin.Name, ID = incoming.Skin.ID };

            result.KeyBindings = incoming.KeyBindings
                                         .Where(b => wanted.Contains((BanchosucksSettingsSection.KeyBindings, b.Ruleset, b.Variant, b.Action.ToString(CultureInfo.InvariantCulture))))
                                         .Select(b => b.Clone()).ToList();

            result.RulesetSettings = incoming.RulesetSettings
                                             .Where(s => wanted.Contains((BanchosucksSettingsSection.RulesetSettings, s.Ruleset, s.Variant, s.Key)))
                                             .Select(s => new BanchosucksRulesetSettingEntry { Ruleset = s.Ruleset, Variant = s.Variant, Key = s.Key, Value = s.Value }).ToList();

            result.KeyProfiles = incoming.KeyProfiles
                                         .Where(p => wanted.Contains((BanchosucksSettingsSection.KeyProfiles, p.Ruleset, p.Variant, p.Name)))
                                         .Select(p => p.Clone()).ToList();

            return result;
        }

        /// <summary>
        /// Whether two setting texts mean the same value ("0.7" and "0.70", "True" and "true").
        /// </summary>
        public static bool SameValue(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal))
                return true;

            if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                return Math.Abs(x - y) < 1e-6;

            if (bool.TryParse(a, out bool p) && bool.TryParse(b, out bool q))
                return p == q;

            return false;
        }

        /// <summary>
        /// A setting text as the preview shows it: numbers without the padding of the config file ("0.0" becomes "0").
        /// </summary>
        public static string Display(string value)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number)
                ? number.ToString("0.####", CultureInfo.InvariantCulture)
                : value;

        private static void compareValues(BanchosucksSettingsSection section, IDictionary<string, string> current, IDictionary<string, string> incoming, List<BanchosucksSettingsChange> changes)
        {
            foreach (var pair in incoming)
            {
                if (current.TryGetValue(pair.Key, out string? existing) && !SameValue(existing, pair.Value))
                    changes.Add(new BanchosucksSettingsChange(section, string.Empty, 0, pair.Key, Display(existing), Display(pair.Value)));
            }
        }

        private static bool sameSkin(BanchosucksSkinReference? current, BanchosucksSkinReference incoming)
        {
            if (current == null)
                return false;

            if (current.ID != null && incoming.ID != null)
                return current.ID == incoming.ID;

            return string.Equals(current.Name, incoming.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<(string, int, int), BanchosucksKeyBindingEntry> toLookup(IEnumerable<BanchosucksKeyBindingEntry> entries)
        {
            var lookup = new Dictionary<(string, int, int), BanchosucksKeyBindingEntry>();

            foreach (var entry in entries)
                lookup.TryAdd((entry.Ruleset, entry.Variant, entry.Action), entry);

            return lookup;
        }
    }
}

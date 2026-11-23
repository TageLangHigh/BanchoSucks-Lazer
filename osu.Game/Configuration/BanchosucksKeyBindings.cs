// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: one stored key binding row as the database has it (one row per slot of an action).
    /// <see cref="Ruleset"/> is empty for the global bindings; <see cref="Keys"/> is the database text ("90" or "3,104").
    /// </summary>
    public readonly record struct BanchosucksBindingRow(Guid ID, string Ruleset, int Variant, int Action, string Keys);

    /// <summary>
    /// Banchosucks: the bindings of one action, as written to settings exports and key profiles: one text per slot,
    /// keys by name joined with "+" ("Z", "Control+Shift+S", "None" for an empty slot).
    /// </summary>
    public class BanchosucksKeyBindingEntry
    {
        /// <summary>
        /// Short name of the ruleset ("osu", "taiko", "fruits", "mania"); empty for global bindings.
        /// </summary>
        public string Ruleset = string.Empty;

        /// <summary>
        /// The key binding variant of the ruleset (osu!mania: 4 = 4K, 1008 = 4K + 4K); 0 for global bindings.
        /// </summary>
        public int Variant;

        /// <summary>
        /// The number of the action, as the database stores it.
        /// </summary>
        public int Action;

        public List<string> Keys = new List<string>();

        public BanchosucksKeyBindingEntry Clone() => new BanchosucksKeyBindingEntry
        {
            Ruleset = Ruleset,
            Variant = Variant,
            Action = Action,
            Keys = new List<string>(Keys),
        };
    }

    /// <summary>
    /// Banchosucks: turns key bindings into text and back, and works out which database rows an import or a key
    /// profile has to rewrite. Shared by the settings export (F46), the key profiles (F47) and the osu!stable
    /// takeover (F44); no database or game access in here.
    /// </summary>
    public static class BanchosucksKeyBindings
    {
        public const string NONE = "None";

        private const char separator = '+';

        /// <summary>
        /// Keys that the game itself refuses for gameplay bindings (see <c>RealmKeyBindingStore.CheckValidForGameplay</c>).
        /// </summary>
        private static readonly InputKey[] banned_for_gameplay =
        {
            InputKey.MouseWheelDown,
            InputKey.MouseWheelLeft,
            InputKey.MouseWheelUp,
            InputKey.MouseWheelRight,
        };

        /// <summary>
        /// "Control+Shift+S" for a combination, "None" for an empty one.
        /// </summary>
        public static string ToText(KeyCombination combination)
        {
            var keys = combination.Keys.Where(k => k != InputKey.None).ToArray();
            return keys.Length == 0 ? NONE : string.Join(separator, keys.Select(k => k.ToString()));
        }

        /// <summary>
        /// The text of a database value ("90" becomes "Z").
        /// </summary>
        public static string ToText(string databaseKeys) => ToText(new KeyCombination(databaseKeys));

        /// <summary>
        /// Reads a text written by <see cref="ToText(KeyCombination)"/>. Key names are matched exactly; a name this
        /// client does not know (a file from a newer client) makes the whole text unreadable, so that half a
        /// combination is never bound.
        /// </summary>
        public static bool TryParse(string? text, out KeyCombination combination)
        {
            combination = new KeyCombination(InputKey.None);

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var keys = new List<InputKey>();

            foreach (string part in text.Split(separator))
            {
                string name = part.Trim();

                // numbers and comma lists would be accepted by Enum.TryParse and could name anything
                if (name.Length == 0 || char.IsDigit(name[0]) || name[0] == '-' || name.Contains(','))
                    return false;

                if (!Enum.TryParse(name, false, out InputKey key) || !Enum.IsDefined(key))
                    return false;

                if (key != InputKey.None)
                    keys.Add(key);
            }

            combination = keys.Count == 0 ? new KeyCombination(InputKey.None) : new KeyCombination(keys.ToArray());
            return true;
        }

        public static bool IsValidForGameplay(KeyCombination combination) => !combination.Keys.Any(k => banned_for_gameplay.Contains(k));

        /// <summary>
        /// Groups database rows into one entry per action, keeping the order of the rows as the order of the slots.
        /// </summary>
        public static List<BanchosucksKeyBindingEntry> FromRows(IEnumerable<BanchosucksBindingRow> rows)
        {
            var entries = new List<BanchosucksKeyBindingEntry>();
            var lookup = new Dictionary<(string, int, int), BanchosucksKeyBindingEntry>();

            foreach (var row in rows)
            {
                var key = (row.Ruleset, row.Variant, row.Action);

                if (!lookup.TryGetValue(key, out var entry))
                {
                    entry = new BanchosucksKeyBindingEntry { Ruleset = row.Ruleset, Variant = row.Variant, Action = row.Action };
                    lookup[key] = entry;
                    entries.Add(entry);
                }

                entry.Keys.Add(ToText(row.Keys));
            }

            return entries;
        }

        /// <summary>
        /// What the slots of an action look like after <paramref name="incoming"/> was applied to it: slot by slot the
        /// incoming text, an empty slot where the incoming entry has fewer, the old text where the incoming one cannot
        /// be used. Slots the incoming entry has too many of are dropped (this client has fewer slots for the action).
        /// </summary>
        public static List<string> Merge(BanchosucksKeyBindingEntry current, BanchosucksKeyBindingEntry incoming)
        {
            var result = new List<string>(current.Keys.Count);
            bool gameplay = !string.IsNullOrEmpty(current.Ruleset);

            for (int i = 0; i < current.Keys.Count; i++)
            {
                if (i >= incoming.Keys.Count)
                {
                    result.Add(NONE);
                    continue;
                }

                if (!TryParse(incoming.Keys[i], out var combination) || (gameplay && !IsValidForGameplay(combination)))
                    result.Add(current.Keys[i]);
                else
                    result.Add(ToText(combination));
            }

            return result;
        }

        /// <summary>
        /// The database rows that have to be rewritten so that the stored bindings match <paramref name="incoming"/>.
        /// Actions the database does not have (another client version, a ruleset that is not installed) are skipped
        /// and counted; actions the incoming list does not mention keep their bindings.
        /// </summary>
        /// <returns>Row ID and the new database text of every row that changes.</returns>
        public static List<(Guid ID, string Keys)> Plan(IEnumerable<BanchosucksBindingRow> currentRows, IEnumerable<BanchosucksKeyBindingEntry> incoming, out int skipped)
        {
            var rows = currentRows.ToList();
            var currentEntries = FromRows(rows).ToDictionary(e => (e.Ruleset, e.Variant, e.Action));
            var rowsByAction = rows.GroupBy(r => (r.Ruleset, r.Variant, r.Action)).ToDictionary(g => g.Key, g => g.ToList());

            var plan = new List<(Guid, string)>();
            skipped = 0;

            foreach (var entry in incoming)
            {
                var key = (entry.Ruleset, entry.Variant, entry.Action);

                if (!currentEntries.TryGetValue(key, out var current))
                {
                    skipped++;
                    continue;
                }

                var merged = Merge(current, entry);
                var actionRows = rowsByAction[key];

                for (int i = 0; i < actionRows.Count; i++)
                {
                    if (merged[i] == current.Keys[i])
                        continue;

                    TryParse(merged[i], out var combination);
                    plan.Add((actionRows[i].ID, combination.ToString()));
                }
            }

            return plan;
        }

        /// <summary>
        /// The entries of one ruleset and variant, copied.
        /// </summary>
        public static List<BanchosucksKeyBindingEntry> Select(IEnumerable<BanchosucksKeyBindingEntry> entries, string ruleset, int variant)
            => entries.Where(e => e.Ruleset == ruleset && e.Variant == variant).Select(e => e.Clone()).ToList();
    }
}

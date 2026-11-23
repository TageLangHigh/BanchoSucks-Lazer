// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Input.Bindings;

namespace osu.Game.Input
{
    /// <summary>
    /// Banchosucks: saves the key bindings of a ruleset and variant under a name and switches between such
    /// profiles ("Tasten-Profile"). The profiles live in a JSON file in the game data folder
    /// (<see cref="BanchosucksKeyProfiles"/>); switching rewrites the binding rows in the database, which the
    /// running key binding containers pick up at once.
    /// </summary>
    /// <remarks>
    /// Call from the update thread.
    /// </remarks>
    public class BanchosucksKeyProfileManager
    {
        private readonly RealmAccess realm;
        private readonly Storage storage;

        public BanchosucksKeyProfileManager(RealmAccess realm, Storage storage)
        {
            this.realm = realm;
            this.storage = storage;
        }

        /// <summary>
        /// The saved profiles of a ruleset and variant, the automatic backup last.
        /// </summary>
        public List<BanchosucksKeyProfile> For(string ruleset, int variant) => BanchosucksKeyProfiles.Load(storage).For(ruleset, variant).ToList();

        /// <summary>
        /// The bindings the database currently holds for a ruleset and variant.
        /// </summary>
        public List<BanchosucksKeyBindingEntry> Current(string ruleset, int variant)
            => realm.Run(r =>
            {
                r.Refresh();
                return BanchosucksKeyBindings.FromRows(rows(r, ruleset, variant));
            });

        /// <summary>
        /// Saves the current bindings under a name; a profile of that name is replaced.
        /// </summary>
        /// <returns>The profile, or null when the name is empty or reserved.</returns>
        public BanchosucksKeyProfile? SaveCurrent(string ruleset, int variant, string? name)
        {
            string? cleaned = BanchosucksKeyProfiles.CleanName(name);

            if (cleaned == null || string.Equals(cleaned, BanchosucksKeyProfiles.BACKUP_NAME, StringComparison.OrdinalIgnoreCase))
                return null;

            var profiles = BanchosucksKeyProfiles.Load(storage);
            var profile = profiles.Save(ruleset, variant, cleaned, Current(ruleset, variant));

            if (profile != null)
                profiles.Store(storage);

            return profile;
        }

        public bool Delete(string ruleset, int variant, string name)
        {
            var profiles = BanchosucksKeyProfiles.Load(storage);

            if (!profiles.Delete(ruleset, variant, name))
                return false;

            profiles.Store(storage);
            return true;
        }

        /// <summary>
        /// How many binding slots switching to a profile would rewrite.
        /// </summary>
        public int CountChanges(BanchosucksKeyProfile profile)
            => realm.Run(r => BanchosucksKeyBindings.Plan(rows(r, profile.Ruleset, profile.Variant), profile.Bindings, out _).Count);

        /// <summary>
        /// Replaces the bindings of the profile's ruleset and variant with the profile. The bindings that were
        /// active until now are kept as the profile <see cref="BanchosucksKeyProfiles.BACKUP_NAME"/> first.
        /// Actions the profile does not mention (added by a later client version) keep their keys.
        /// </summary>
        /// <returns>The number of binding slots that changed.</returns>
        public int Apply(BanchosucksKeyProfile profile)
        {
            // the profile may be the backup itself, which is about to be overwritten
            profile = profile.Clone();

            if (CountChanges(profile) == 0)
                return 0;

            var profiles = BanchosucksKeyProfiles.Load(storage);
            profiles.Save(profile.Ruleset, profile.Variant, BanchosucksKeyProfiles.BACKUP_NAME, Current(profile.Ruleset, profile.Variant));
            profiles.Store(storage);

            int changed = 0;

            realm.Write(r =>
            {
                var plan = BanchosucksKeyBindings.Plan(rows(r, profile.Ruleset, profile.Variant), profile.Bindings, out _);

                foreach (var (id, keys) in plan)
                    r.Find<RealmKeyBinding>(id)!.KeyCombinationString = keys;

                changed = plan.Count;
            });

            return changed;
        }

        private static List<BanchosucksBindingRow> rows(Realms.Realm r, string ruleset, int variant)
            => r.All<RealmKeyBinding>().Where(b => b.RulesetName == ruleset && b.Variant == variant).AsEnumerable()
                .Select(b => new BanchosucksBindingRow(b.ID, b.RulesetName ?? string.Empty, b.Variant ?? 0, b.ActionInt, b.KeyCombinationString))
                .ToList();
    }
}

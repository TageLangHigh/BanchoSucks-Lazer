// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Key profiles (F47): the list of saved profiles and switching between them in the database.
    /// </summary>
    [TestFixture]
    public partial class BanchosucksKeyProfilesTest : RealmTest
    {
        #region The list

        [Test]
        public void TestProfilesAreKeptPerRulesetAndVariant()
        {
            var profiles = new BanchosucksKeyProfiles();

            Assert.That(profiles.Save("mania", 4, "Standard", new[] { entry("mania", 4, 0, "D"), entry("mania", 7, 0, "S"), entry("osu", 0, 0, "Z") }), Is.Not.Null);
            Assert.That(profiles.Save("mania", 7, "Standard", new[] { entry("mania", 4, 0, "D"), entry("mania", 7, 0, "S") }), Is.Not.Null);
            Assert.That(profiles.Save("mania", 4, BanchosucksKeyProfiles.BACKUP_NAME, new[] { entry("mania", 4, 0, "A") }), Is.Not.Null);
            Assert.That(profiles.Save("mania", 4, "Alternativ", new[] { entry("mania", 4, 0, "Q") }), Is.Not.Null);

            Assert.That(profiles.For("mania", 4).Select(p => p.Name), Is.EqualTo(new[] { "Alternativ", "Standard", BanchosucksKeyProfiles.BACKUP_NAME }), "backup last");
            Assert.That(profiles.For("mania", 7).Select(p => p.Name), Is.EqualTo(new[] { "Standard" }));
            Assert.That(profiles.For("osu", 0), Is.Empty);

            // a profile only holds the bindings of its own ruleset and variant
            var fourKeys = profiles.Find("mania", 4, "standard")!;
            Assert.That(fourKeys.Bindings.Select(b => (b.Ruleset, b.Variant, b.Keys.Single())), Is.EqualTo(new[] { ("mania", 4, "D") }));
            Assert.That(profiles.Find("mania", 7, "Standard")!.Bindings.Single().Keys.Single(), Is.EqualTo("S"));
        }

        [Test]
        public void TestSameNameIsReplacedAndDeleteRemoves()
        {
            var profiles = new BanchosucksKeyProfiles();

            profiles.Save("osu", 0, "Mein Profil", new[] { entry("osu", 0, 0, "Z") });
            profiles.Save("osu", 0, "  mein profil ", new[] { entry("osu", 0, 0, "A") });

            Assert.That(profiles.Profiles, Has.Count.EqualTo(1));
            Assert.That(profiles.Profiles[0].Name, Is.EqualTo("mein profil"));
            Assert.That(profiles.Profiles[0].Bindings.Single().Keys.Single(), Is.EqualTo("A"));

            Assert.That(profiles.Delete("osu", 1, "Mein Profil"), Is.False, "other variant");
            Assert.That(profiles.Delete("osu", 0, "MEIN PROFIL"), Is.True);
            Assert.That(profiles.Profiles, Is.Empty);
        }

        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("   ", null)]
        [TestCase(" Links ", "Links")]
        [TestCase("Zeile\r\nzwei", "Zeilezwei")]
        public void TestNames(string? typed, string? expected)
        {
            Assert.That(BanchosucksKeyProfiles.CleanName(typed), Is.EqualTo(expected));
        }

        [Test]
        public void TestLongNamesAreCut()
        {
            Assert.That(BanchosucksKeyProfiles.CleanName(new string('x', 200)), Has.Length.EqualTo(BanchosucksKeyProfiles.MAX_NAME_LENGTH));
            Assert.That(new BanchosucksKeyProfiles().Save("osu", 0, "\t", new[] { entry("osu", 0, 0, "Z") }), Is.Null);
        }

        [Test]
        public void TestFileRoundTrip()
        {
            var profiles = new BanchosucksKeyProfiles();
            profiles.Save("mania", 4, "Links „oben“", new[] { entry("mania", 4, 0, "D"), entry("mania", 4, 1, "F", "None") });
            profiles.Save("osu", 0, "Tablet", new[] { entry("osu", 0, 0, "Z", "MouseLeft") });

            var read = BanchosucksKeyProfiles.Deserialise(profiles.Serialise());

            Assert.That(read.Profiles.Select(p => (p.Name, p.Ruleset, p.Variant)), Is.EqualTo(new[] { ("Links „oben“", "mania", 4), ("Tablet", "osu", 0) }));
            Assert.That(read.Find("mania", 4, "Links „oben“")!.SameContentAs(profiles.Find("mania", 4, "Links „oben“")!), Is.True);
            Assert.That(read.Find("osu", 0, "Tablet")!.Bindings.Single().Keys, Is.EqualTo(new[] { "Z", "MouseLeft" }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("kaputt")]
        [TestCase("[1, 2]")]
        [TestCase(@"{ ""profiles"": 5 }")]
        public void TestBrokenFilesGiveAnEmptyList(string? text)
        {
            Assert.That(BanchosucksKeyProfiles.Deserialise(text).Profiles, Is.Empty);
        }

        [Test]
        public void TestBrokenEntriesAreLeftOut()
        {
            var read = BanchosucksKeyProfiles.Deserialise(@"{ ""version"": 7, ""profiles"": [
                { ""name"": ""gut"", ""ruleset"": ""osu"", ""variant"": 0, ""bindings"": [ { ""ruleset"": ""osu"", ""variant"": 0, ""action"": 0, ""keys"": [""Z""] } ] },
                { ""name"": """", ""ruleset"": ""osu"", ""variant"": 0, ""bindings"": [ { ""ruleset"": ""osu"", ""variant"": 0, ""action"": 0, ""keys"": [""Z""] } ] },
                { ""name"": ""ohne Modus"", ""variant"": 0, ""bindings"": [ { ""ruleset"": ""osu"", ""variant"": 0, ""action"": 0, ""keys"": [""Z""] } ] },
                { ""name"": ""ohne Tasten"", ""ruleset"": ""osu"", ""variant"": 0, ""bindings"": [] },
                { ""name"": ""falscher Modus"", ""ruleset"": ""osu"", ""variant"": 0, ""bindings"": [ { ""ruleset"": ""taiko"", ""variant"": 0, ""action"": 0, ""keys"": [""Z""] } ] },
                { ""name"": ""Zahl als Taste"", ""ruleset"": ""osu"", ""variant"": 0, ""bindings"": [ { ""ruleset"": ""osu"", ""variant"": 0, ""action"": 0, ""keys"": [90] } ] },
                ""text"", 5, null
            ] }");

            Assert.That(read.Profiles.Select(p => p.Name), Is.EqualTo(new[] { "gut" }));
        }

        #endregion

        #region Switching in the database

        [Test]
        public void TestSaveSwitchAndSwitchBack()
        {
            RunTestWithRealm((realm, storage) =>
            {
                registerBindings(realm);

                var manager = new BanchosucksKeyProfileManager(realm, storage);

                // 4K: D F J K by default
                Assert.That(keysOf(realm, "mania", 4), Is.EqualTo(new[] { InputKey.D, InputKey.F, InputKey.J, InputKey.K }));

                Assert.That(manager.SaveCurrent("mania", 4, "Mitte"), Is.Not.Null);

                setKeys(realm, "mania", 4, InputKey.A, InputKey.S, InputKey.K, InputKey.L);
                Assert.That(manager.SaveCurrent("mania", 4, "Außen"), Is.Not.Null);

                // a new manager reads the same file
                manager = new BanchosucksKeyProfileManager(realm, storage);
                Assert.That(manager.For("mania", 4).Select(p => p.Name), Is.EqualTo(new[] { "Außen", "Mitte" }));
                Assert.That(manager.For("mania", 7), Is.Empty);

                var middle = manager.For("mania", 4).Single(p => p.Name == "Mitte");
                var outer = manager.For("mania", 4).Single(p => p.Name == "Außen");

                Assert.That(manager.CountChanges(outer), Is.Zero, "the active bindings");
                Assert.That(manager.CountChanges(middle), Is.EqualTo(4));

                var sevenKeysBefore = keysOf(realm, "mania", 7);
                var osuBefore = keysOf(realm, "osu", 0);

                Assert.That(manager.Apply(middle), Is.EqualTo(4));

                Assert.That(keysOf(realm, "mania", 4), Is.EqualTo(new[] { InputKey.D, InputKey.F, InputKey.J, InputKey.K }));
                Assert.That(keysOf(realm, "mania", 7), Is.EqualTo(sevenKeysBefore), "7K is a separate variant");
                Assert.That(keysOf(realm, "osu", 0), Is.EqualTo(osuBefore));

                // what was replaced is kept as the backup profile and can be switched back to
                var backup = manager.For("mania", 4).Last();

                Assert.That(backup.Name, Is.EqualTo(BanchosucksKeyProfiles.BACKUP_NAME));
                Assert.That(backup.SameContentAs(outer), Is.True);

                Assert.That(manager.Apply(backup), Is.EqualTo(4));
                Assert.That(keysOf(realm, "mania", 4), Is.EqualTo(new[] { InputKey.A, InputKey.S, InputKey.K, InputKey.L }));
                Assert.That(manager.For("mania", 4).Last().SameContentAs(middle), Is.True, "backup now holds what was replaced this time");

                // switching to what is already active changes nothing and leaves the backup alone
                Assert.That(manager.Apply(outer), Is.Zero);
                Assert.That(manager.For("mania", 4).Last().SameContentAs(middle), Is.True);

                Assert.That(manager.Delete("mania", 4, "Mitte"), Is.True);
                Assert.That(manager.Delete("mania", 4, "Mitte"), Is.False);
                Assert.That(manager.For("mania", 4).Select(p => p.Name), Is.EqualTo(new[] { "Außen", BanchosucksKeyProfiles.BACKUP_NAME }));
                Assert.That(keysOf(realm, "mania", 4), Is.EqualTo(new[] { InputKey.A, InputKey.S, InputKey.K, InputKey.L }), "deleting a profile does not touch the bindings");
            });
        }

        [Test]
        public void TestSecondSlotsAndUnknownActions()
        {
            RunTestWithRealm((realm, storage) =>
            {
                registerBindings(realm);

                var manager = new BanchosucksKeyProfileManager(realm, storage);
                manager.SaveCurrent("osu", 0, "Standard");

                // the player rebinds the key and clears the mouse button of the left action
                realm.Write(r =>
                {
                    var left = bindingsOf(r, "osu", 0).Where(b => b.ActionInt == (int)OsuAction.LeftButton).ToList();
                    left[0].KeyCombination = new KeyCombination(InputKey.A);
                    left[1].KeyCombination = new KeyCombination(InputKey.None);
                });

                var profile = manager.For("osu", 0).Single();

                // an action from another client version does not get in the way
                profile.Bindings.Add(entry("osu", 0, 4711, "B"));

                Assert.That(manager.Apply(profile), Is.EqualTo(2));

                var leftAfter = realm.Run(r => bindingsOf(r, "osu", 0).Where(b => b.ActionInt == (int)OsuAction.LeftButton).Select(b => b.KeyCombination).ToList());
                Assert.That(leftAfter, Is.EqualTo(new[] { new KeyCombination(InputKey.Z), new KeyCombination(InputKey.MouseLeft) }));
            });
        }

        [Test]
        public void TestReservedAndEmptyNamesAreRefused()
        {
            RunTestWithRealm((realm, storage) =>
            {
                registerBindings(realm);

                var manager = new BanchosucksKeyProfileManager(realm, storage);

                Assert.That(manager.SaveCurrent("osu", 0, BanchosucksKeyProfiles.BACKUP_NAME), Is.Null);
                Assert.That(manager.SaveCurrent("osu", 0, "  "), Is.Null);
                Assert.That(manager.SaveCurrent("osu", 0, null), Is.Null);
                Assert.That(manager.For("osu", 0), Is.Empty);
            });
        }

        #endregion

        #region Helpers

        private static readonly Ruleset[] rulesets = { new OsuRuleset(), new ManiaRuleset() };

        private static void registerBindings(RealmAccess realm)
            => new RealmKeyBindingStore(realm, new ReadableKeyCombinationProvider()).Register(new GlobalActionContainer(null), rulesets.Select(r => r.RulesetInfo));

        private static List<RealmKeyBinding> bindingsOf(Realms.Realm realm, string ruleset, int variant)
            => realm.All<RealmKeyBinding>().AsEnumerable().Where(b => b.RulesetName == ruleset && b.Variant == variant).ToList();

        /// <summary>
        /// The first slot of every action of a ruleset and variant (osu!mania has a second, empty slot per key).
        /// </summary>
        private static List<RealmKeyBinding> firstSlots(Realms.Realm realm, string ruleset, int variant)
            => bindingsOf(realm, ruleset, variant).GroupBy(b => b.ActionInt).OrderBy(g => g.Key).Select(g => g.First()).ToList();

        private static InputKey[] keysOf(RealmAccess realm, string ruleset, int variant)
            => realm.Run(r => firstSlots(r, ruleset, variant).Select(b => b.KeyCombination.Keys.Single()).ToArray());

        private static void setKeys(RealmAccess realm, string ruleset, int variant, params InputKey[] keys)
            => realm.Write(r =>
            {
                var rows = firstSlots(r, ruleset, variant);

                for (int i = 0; i < keys.Length; i++)
                    rows[i].KeyCombination = new KeyCombination(keys[i]);
            });

        private static BanchosucksKeyBindingEntry entry(string ruleset, int variant, int action, params string[] keys)
            => new BanchosucksKeyBindingEntry { Ruleset = ruleset, Variant = variant, Action = action, Keys = keys.ToList() };

        #endregion
    }
}

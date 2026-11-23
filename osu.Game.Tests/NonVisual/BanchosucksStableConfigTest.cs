// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Taking over osu!stable settings (F44): reading the config text and mapping every value onto lazer.
    /// </summary>
    [TestFixture]
    public partial class BanchosucksStableConfigTest : RealmTest
    {
        /// <summary>
        /// A stable config as the client writes it (shortened), with the lines that must never be read.
        /// </summary>
        public const string SAMPLE = @"# osu! configuration for GeheimerStableName
# last updated on Sunday, 22 November 2026

BeatmapDirectory = Songs
VolumeUniversal = 85
VolumeEffect = 60
VolumeMusic = 100
Offset = 12
DimLevel = 30
CursorSize = 1.25
AutomaticCursorSizing = 1
MouseSpeed = 1.5
RawInput = 1
Skin = rafis-2017
Username = GeheimerStableName
Password = 5f4dcc3b5aa765d61d8327deb882cf99
SaveUsername = 1
SavePassword = 1
LastVersion = b20261101.2
Language = de
keyOsuLeft = A
keyOsuRight = S
keyOsuSmoke = D3
keyFruitsDash = LeftShift
keyFruitsLeft = Left
keyFruitsRight = Right
keyTaikoInnerLeft = X
keyTaikoInnerRight = OemComma
keyTaikoOuterLeft = Z
keyTaikoOuterRight = OemPeriod
keyPause = Escape
keyToggleChat = F8
ManiaLayouts4K = A S K L
ManiaLayouts7K = S D F Space J K L
";

        #region Reading

        [Test]
        public void TestOnlyTheListedKeysAreRead()
        {
            var config = BanchosucksStableConfig.Parse(SAMPLE);

            Assert.That(config.Values.Keys, Is.SubsetOf(BanchosucksStableConfig.ReadKeys));
            Assert.That(config.Values, Has.Count.EqualTo(22));

            // the stable account and everything else stays in the file
            foreach (string forbidden in new[] { "Username", "Password", "SaveUsername", "SavePassword", "BeatmapDirectory", "LastVersion", "Language", "keyPause" })
            {
                Assert.That(config.Values.ContainsKey(forbidden), Is.False, forbidden);
                Assert.That(BanchosucksStableConfig.ReadKeys, Does.Not.Contain(forbidden), forbidden);
            }

            Assert.That(config.Values.Values, Has.None.Contains("GeheimerStableName"));
            Assert.That(config.Values.Values, Has.None.Contains("5f4dcc3b5aa765d61d8327deb882cf99"));

            // and it does not reach what is built from it
            string mapped = config.Map(defaultSetup(), out _).Serialise();

            Assert.That(mapped, Does.Not.Contain("GeheimerStableName"));
            Assert.That(mapped, Does.Not.Contain("5f4dcc3b5aa765d61d8327deb882cf99"));
        }

        [Test]
        public void TestLineFormats()
        {
            var config = BanchosucksStableConfig.Parse("  Offset=5 \r\n#DimLevel = 10\r\nVolumeMusic  =  40\r\nCursorSize\r\n= 3\r\n\r\nskin = Mein Skin = neu \r\n");

            Assert.That(config.Values["Offset"], Is.EqualTo("5"));
            Assert.That(config.Values["VolumeMusic"], Is.EqualTo("40"));
            Assert.That(config.Values["Skin"], Is.EqualTo("Mein Skin = neu"));
            Assert.That(config.Values, Has.Count.EqualTo(3));

            Assert.That(BanchosucksStableConfig.Parse(null).Values, Is.Empty);
            Assert.That(BanchosucksStableConfig.Parse(string.Empty).Values, Is.Empty);
        }

        [Test]
        public void TestFindConfigFile()
        {
            Assert.That(BanchosucksStableConfig.FindConfigFile(new[] { "osu!.cfg", "osu!.Anna.cfg", "osu!.Ben.cfg" }, "Ben"), Is.EqualTo("osu!.Ben.cfg"));
            Assert.That(BanchosucksStableConfig.FindConfigFile(new[] { "./osu!.anna.cfg", "./osu!.Ben.cfg" }, "Anna"), Is.EqualTo("osu!.anna.cfg"), "case of the user name");
            Assert.That(BanchosucksStableConfig.FindConfigFile(new[] { "osu!.cfg", "osu!.Anna.cfg" }, "Ben"), Is.EqualTo("osu!.Anna.cfg"), "the only user config");
            Assert.That(BanchosucksStableConfig.FindConfigFile(new[] { "osu!.cfg", "osu!.Anna.cfg", "osu!.Carl.cfg" }, "Ben"), Is.Null, "would be a guess");
            Assert.That(BanchosucksStableConfig.FindConfigFile(new[] { "osu!.cfg", "osu!.db", "scores.db" }, "Ben"), Is.Null);
            Assert.That(BanchosucksStableConfig.FindConfigFile(Enumerable.Empty<string>(), "Ben"), Is.Null);
        }

        #endregion

        #region Values

        [Test]
        public void TestOffsetKeepsTheUnitAndTurnsTheSign()
        {
            // stable +12 ms: objects 12 ms later. lazer -12 ms: gameplay 12 ms later.
            Assert.That(BanchosucksStableConfig.ConvertOffset(12), Is.EqualTo(-12));
            Assert.That(BanchosucksStableConfig.ConvertOffset(-25), Is.EqualTo(25));
            Assert.That(BanchosucksStableConfig.ConvertOffset(0), Is.EqualTo(0));

            Assert.That(map("Offset = 12").Game[nameof(OsuSetting.AudioOffset)], Is.EqualTo("-12"));
            Assert.That(map("Offset = -300").Game[nameof(OsuSetting.AudioOffset)], Is.EqualTo("300"));
            Assert.That(map("Offset = 0").Game[nameof(OsuSetting.AudioOffset)], Is.EqualTo("0"));
        }

        [Test]
        public void TestPercentBecomesFraction()
        {
            Assert.That(BanchosucksStableConfig.ConvertPercent(85), Is.EqualTo(0.85));
            Assert.That(BanchosucksStableConfig.ConvertPercent(0), Is.EqualTo(0));
            Assert.That(BanchosucksStableConfig.ConvertPercent(100), Is.EqualTo(1));

            var document = map(SAMPLE);

            Assert.That(document.Framework[nameof(FrameworkSetting.VolumeUniversal)], Is.EqualTo("0.85"));
            Assert.That(document.Framework[nameof(FrameworkSetting.VolumeEffect)], Is.EqualTo("0.6"));
            Assert.That(document.Framework[nameof(FrameworkSetting.VolumeMusic)], Is.EqualTo("1"));
            Assert.That(document.Game[nameof(OsuSetting.DimLevel)], Is.EqualTo("0.3"));
        }

        [Test]
        public void TestMouseCursorAndSkin()
        {
            var document = map(SAMPLE);

            Assert.That(document.Mouse[BanchosucksSettingsAllowList.MOUSE_SENSITIVITY], Is.EqualTo("1.5"));
            Assert.That(document.Mouse[BanchosucksSettingsAllowList.MOUSE_RELATIVE_MODE], Is.EqualTo("True"));
            Assert.That(document.Game[nameof(OsuSetting.GameplayCursorSize)], Is.EqualTo("1.25"));
            Assert.That(document.Game[nameof(OsuSetting.AutoCursorSize)], Is.EqualTo("True"));
            Assert.That(document.Skin!.Name, Is.EqualTo("rafis-2017"));
            Assert.That(document.Skin.ID, Is.Null);

            // nothing but what stable has a value for
            Assert.That(document.Game.Keys, Is.EquivalentTo(new[]
            {
                nameof(OsuSetting.AudioOffset), nameof(OsuSetting.DimLevel), nameof(OsuSetting.GameplayCursorSize), nameof(OsuSetting.AutoCursorSize),
            }));

            // stable's built-in skin is lazer's classic skin
            var builtIn = map("Skin = Default");
            Assert.That(builtIn.Skin!.ID.ToString(), Is.EqualTo("81f02cd3-eec6-4865-ac23-fae26a386187"));

            // every mapped setting may be imported at all
            Assert.That(document.Game.Keys.All(BanchosucksSettingsAllowList.IsGameSettingAllowed), Is.True);
            Assert.That(document.Framework.Keys.All(BanchosucksSettingsAllowList.IsFrameworkSettingAllowed), Is.True);
            Assert.That(document.Mouse.Keys.All(BanchosucksSettingsAllowList.IsMouseSettingAllowed), Is.True);
        }

        [Test]
        public void TestDecimalCommaAndFlags()
        {
            Assert.That(map("MouseSpeed = 2,25").Mouse[BanchosucksSettingsAllowList.MOUSE_SENSITIVITY], Is.EqualTo("2.25"));
            Assert.That(map("RawInput = 0").Mouse[BanchosucksSettingsAllowList.MOUSE_RELATIVE_MODE], Is.EqualTo("False"));
            Assert.That(map("RawInput = True").Mouse[BanchosucksSettingsAllowList.MOUSE_RELATIVE_MODE], Is.EqualTo("True"));
        }

        [Test]
        public void TestUnusableValuesAreReportedNotGuessed()
        {
            var document = BanchosucksStableConfig.Parse(@"Offset = bald
VolumeMusic = 250
DimLevel = -5
MouseSpeed = NaN
RawInput = vielleicht
CursorSize = 1
keyOsuLeft = KeineTaste
keyOsuRight = 17
ManiaLayouts5K = D F J K
ManiaLayouts4K = D F J OemWasAuchImmer
").Map(defaultSetup(), out var notes);

            Assert.That(notes.All(n => n.Type == BanchosucksTransferNoteType.StableValueUnusable), Is.True);
            Assert.That(notes.Select(n => n.Detail), Is.EquivalentTo(new[]
            {
                "Offset", "VolumeMusic", "DimLevel", "MouseSpeed", "RawInput", "keyOsuLeft", "keyOsuRight", "ManiaLayouts5K", "ManiaLayouts4K",
            }));

            Assert.That(document.Game.Keys, Is.EquivalentTo(new[] { nameof(OsuSetting.GameplayCursorSize) }));
            Assert.That(document.Framework, Is.Empty);
            Assert.That(document.Mouse, Is.Empty);
            Assert.That(document.KeyBindings, Is.Empty);
        }

        #endregion

        #region Keys

        [Test]
        public void TestActionNumbersMatchTheRulesets()
        {
            // osu.Game cannot see the rulesets' enums; this is where the numbers are checked
            var expected = new Dictionary<string, (string, int)>
            {
                ["keyOsuLeft"] = ("osu", (int)OsuAction.LeftButton),
                ["keyOsuRight"] = ("osu", (int)OsuAction.RightButton),
                ["keyOsuSmoke"] = ("osu", (int)OsuAction.Smoke),
                ["keyTaikoOuterLeft"] = ("taiko", (int)TaikoAction.LeftRim),
                ["keyTaikoInnerLeft"] = ("taiko", (int)TaikoAction.LeftCentre),
                ["keyTaikoInnerRight"] = ("taiko", (int)TaikoAction.RightCentre),
                ["keyTaikoOuterRight"] = ("taiko", (int)TaikoAction.RightRim),
                ["keyFruitsLeft"] = ("fruits", (int)CatchAction.MoveLeft),
                ["keyFruitsRight"] = ("fruits", (int)CatchAction.MoveRight),
                ["keyFruitsDash"] = ("fruits", (int)CatchAction.Dash),
            };

            Assert.That(BanchosucksStableConfig.KeyActions, Is.EquivalentTo(expected));

            Assert.That(new OsuRuleset().ShortName, Is.EqualTo("osu"));
            Assert.That(new TaikoRuleset().ShortName, Is.EqualTo("taiko"));
            Assert.That(new CatchRuleset().ShortName, Is.EqualTo("fruits"));
            Assert.That(new ManiaRuleset().ShortName, Is.EqualTo("mania"));

            // mania: variant n is the n-key stage, its actions are Key1.. from the left, numbered from 0
            for (int keys = 1; keys <= BanchosucksStableConfig.MAX_MANIA_KEYS; keys++)
            {
                var actions = new ManiaRuleset().GetDefaultKeyBindings(keys).Select(b => (ManiaAction)b.Action).Distinct().ToArray();
                Assert.That(actions, Is.EqualTo(Enumerable.Range(0, keys).Select(i => (ManiaAction)i).ToArray()), $"{keys}K");
            }

            Assert.That((int)ManiaAction.Key1, Is.Zero);
        }

        [Test]
        public void TestStableKeysGoIntoTheFirstSlot()
        {
            var document = map(SAMPLE);

            // osu!: the mouse buttons in the second slot stay
            Assert.That(keysOf(document, "osu", 0, (int)OsuAction.LeftButton), Is.EqualTo(new[] { "A", "MouseLeft" }));
            Assert.That(keysOf(document, "osu", 0, (int)OsuAction.RightButton), Is.EqualTo(new[] { "S", "MouseRight" }));
            Assert.That(keysOf(document, "osu", 0, (int)OsuAction.Smoke), Is.EqualTo(new[] { "Number3" }));

            // taiko: outer = rim, inner = centre
            Assert.That(keysOf(document, "taiko", 0, (int)TaikoAction.LeftRim)[0], Is.EqualTo("Z"));
            Assert.That(keysOf(document, "taiko", 0, (int)TaikoAction.LeftCentre)[0], Is.EqualTo("X"));
            Assert.That(keysOf(document, "taiko", 0, (int)TaikoAction.RightCentre)[0], Is.EqualTo("Comma"));
            Assert.That(keysOf(document, "taiko", 0, (int)TaikoAction.RightRim)[0], Is.EqualTo("Period"));
            Assert.That(keysOf(document, "taiko", 0, (int)TaikoAction.LeftRim)[1], Is.EqualTo("MouseRight"), "second slot stays");

            // catch
            Assert.That(keysOf(document, "fruits", 0, (int)CatchAction.MoveLeft), Is.EqualTo(new[] { "Left", "Left" }));
            Assert.That(keysOf(document, "fruits", 0, (int)CatchAction.MoveRight), Is.EqualTo(new[] { "Right", "Right" }));
            Assert.That(keysOf(document, "fruits", 0, (int)CatchAction.Dash), Is.EqualTo(new[] { "LShift", "MouseLeft" }));

            // mania: per key count, left to right
            Assert.That(Enumerable.Range(0, 4).Select(i => keysOf(document, "mania", 4, i)[0]), Is.EqualTo(new[] { "A", "S", "K", "L" }));
            Assert.That(Enumerable.Range(0, 7).Select(i => keysOf(document, "mania", 7, i)[0]), Is.EqualTo(new[] { "S", "D", "F", "Space", "J", "K", "L" }));
            Assert.That(document.KeyBindings.Any(b => b.Ruleset == "mania" && b.Variant != 4 && b.Variant != 7), Is.False, "other key counts are not mentioned");

            // global bindings are none of stable's business here
            Assert.That(document.KeyBindings.Any(b => b.Ruleset.Length == 0), Is.False);

            // every key text can be bound
            foreach (string text in document.KeyBindings.SelectMany(b => b.Keys))
                Assert.That(BanchosucksKeyBindings.TryParse(text, out _), Is.True, text);
        }

        [Test]
        public void TestAKeyOnlyBelongsToOneAction()
        {
            // lazer's default: MoveLeft = Z + Left, MoveRight = X + Right, Dash = Shift + MouseLeft
            var document = map("keyFruitsLeft = A\nkeyFruitsDash = Left\n");

            Assert.That(keysOf(document, "fruits", 0, (int)CatchAction.Dash), Is.EqualTo(new[] { "Left", "MouseLeft" }));
            Assert.That(keysOf(document, "fruits", 0, (int)CatchAction.MoveLeft), Is.EqualTo(new[] { "A", "None" }), "the arrow now dashes, so it no longer moves");
            Assert.That(keysOf(document, "fruits", 0, (int)CatchAction.MoveRight), Is.EqualTo(new[] { "X", "Right" }), "untouched");

            // swapping two keys is not a conflict
            var swapped = map("keyOsuLeft = X\nkeyOsuRight = Z\n");

            Assert.That(keysOf(swapped, "osu", 0, (int)OsuAction.LeftButton), Is.EqualTo(new[] { "X", "MouseLeft" }));
            Assert.That(keysOf(swapped, "osu", 0, (int)OsuAction.RightButton), Is.EqualTo(new[] { "Z", "MouseRight" }));

            // a key that moves to another action takes the first slot of the action stable did not mention with it
            var moved = map("keyOsuSmoke = Z\n");

            Assert.That(keysOf(moved, "osu", 0, (int)OsuAction.Smoke), Is.EqualTo(new[] { "Z" }));
            Assert.That(keysOf(moved, "osu", 0, (int)OsuAction.LeftButton), Is.EqualTo(new[] { "None", "MouseLeft" }));
        }

        [TestCase("A", InputKey.A)]
        [TestCase("z", InputKey.Z)]
        [TestCase("D0", InputKey.Number0)]
        [TestCase("D9", InputKey.Number9)]
        [TestCase("NumPad4", InputKey.Keypad4)]
        [TestCase("Space", InputKey.Space)]
        [TestCase("Enter", InputKey.Enter)]
        [TestCase("Back", InputKey.BackSpace)]
        [TestCase("LeftShift", InputKey.LShift)]
        [TestCase("RightShift", InputKey.RShift)]
        [TestCase("LeftControl", InputKey.LControl)]
        [TestCase("RightAlt", InputKey.RAlt)]
        [TestCase("OemComma", InputKey.Comma)]
        [TestCase("OemPeriod", InputKey.Period)]
        [TestCase("OemQuestion", InputKey.Slash)]
        [TestCase("OemSemicolon", InputKey.Semicolon)]
        [TestCase("OemQuotes", InputKey.Quote)]
        [TestCase("OemOpenBrackets", InputKey.BracketLeft)]
        [TestCase("OemCloseBrackets", InputKey.BracketRight)]
        [TestCase("OemPipe", InputKey.BackSlash)]
        [TestCase("OemMinus", InputKey.Minus)]
        [TestCase("OemPlus", InputKey.Plus)]
        [TestCase("OemTilde", InputKey.Tilde)]
        [TestCase("OemBackslash", InputKey.NonUSBackSlash)]
        [TestCase("Up", InputKey.Up)]
        [TestCase("F12", InputKey.F12)]
        [TestCase("Add", InputKey.KeypadPlus)]
        [TestCase("Decimal", InputKey.KeypadPeriod)]
        [TestCase("CapsLock", InputKey.CapsLock)]
        public void TestKeyNames(string stableName, InputKey expected)
        {
            Assert.That(BanchosucksStableConfig.MapKey(stableName), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("None")]
        [TestCase("90")]
        [TestCase("A,B")]
        [TestCase("MouseLeft")]
        [TestCase("MouseWheelUp")]
        [TestCase("Shift")]
        [TestCase("Joystick1")]
        [TestCase("OemClear")]
        public void TestKeyNamesWithoutCounterpart(string? stableName)
        {
            Assert.That(BanchosucksStableConfig.MapKey(stableName), Is.Null);
        }

        #endregion

        #region Applying

        [Test]
        public void TestTakingOverIntoARealSetup()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using (var frameworkConfig = new FrameworkConfigManager(storage))
                using (var config = new OsuConfigManager(storage))
                {
                    new RealmKeyBindingStore(realm, new ReadableKeyCombinationProvider()).Register(new GlobalActionContainer(null), rulesets.Select(r => r.RulesetInfo));

                    config.SetValue(OsuSetting.AudioOffset, 0.0);
                    config.SetValue(OsuSetting.DimLevel, 0.7);
                    frameworkConfig.SetValue(FrameworkSetting.VolumeEffect, 1.0);

                    var transfer = new BanchosucksSettingsTransfer(config, frameworkConfig, realm, storage);

                    var incoming = BanchosucksStableConfig.Parse(SAMPLE).Map(transfer.Capture(), out var notes);
                    Assert.That(notes, Is.Empty);

                    var changes = transfer.Preview(incoming, out var previewNotes);

                    // the skin of the sample is not imported here: reported, not applied
                    Assert.That(previewNotes.Select(n => (n.Type, n.Detail)), Is.EqualTo(new[] { (BanchosucksTransferNoteType.SkinMissing, "rafis-2017") }));
                    Assert.That(changes.Any(c => c.Section == BanchosucksSettingsSection.Skin), Is.False);

                    // old and new value of every single setting
                    var offset = changes.Single(c => c.Key == nameof(OsuSetting.AudioOffset));
                    Assert.That((offset.Old, offset.New), Is.EqualTo(("0", "-12")));
                    Assert.That(BanchosucksStableConfig.TitleOf(offset), Is.Not.Null);

                    var left = changes.Single(c => c.Section == BanchosucksSettingsSection.KeyBindings && c.Ruleset == "osu" && c.Key == "0");
                    Assert.That((left.Old, left.New), Is.EqualTo(("Z, MouseLeft", "A, MouseLeft")));
                    Assert.That(BanchosucksStableConfig.TitleOf(left), Is.Null, "keys are shown per mode");

                    // the player leaves everything ticked except the dim level
                    var accepted = changes.Where(c => c.Key != nameof(OsuSetting.DimLevel)).ToList();
                    transfer.Apply(BanchosucksSettingsDiff.Restrict(incoming, accepted));

                    Assert.That(config.Get<double>(OsuSetting.AudioOffset), Is.EqualTo(-12));
                    Assert.That(config.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.7).Within(0.001), "unticked");
                    Assert.That(config.Get<float>(OsuSetting.GameplayCursorSize), Is.EqualTo(1.25f).Within(0.001f));
                    Assert.That(config.Get<bool>(OsuSetting.AutoCursorSize), Is.True);
                    Assert.That(frameworkConfig.Get<double>(FrameworkSetting.VolumeUniversal), Is.EqualTo(0.85).Within(0.001));
                    Assert.That(frameworkConfig.Get<double>(FrameworkSetting.VolumeEffect), Is.EqualTo(0.6).Within(0.001));

                    Assert.That(binding(realm, "osu", 0, (int)OsuAction.LeftButton, 0), Is.EqualTo(new KeyCombination(InputKey.A)));
                    Assert.That(binding(realm, "osu", 0, (int)OsuAction.LeftButton, 1), Is.EqualTo(new KeyCombination(InputKey.MouseLeft)));
                    Assert.That(binding(realm, "osu", 0, (int)OsuAction.Smoke, 0), Is.EqualTo(new KeyCombination(InputKey.Number3)));
                    Assert.That(binding(realm, "taiko", 0, (int)TaikoAction.RightRim, 0), Is.EqualTo(new KeyCombination(InputKey.Period)));
                    Assert.That(binding(realm, "fruits", 0, (int)CatchAction.Dash, 0), Is.EqualTo(new KeyCombination(InputKey.LShift)));
                    Assert.That(binding(realm, "mania", 4, (int)ManiaAction.Key1, 0), Is.EqualTo(new KeyCombination(InputKey.A)));
                    Assert.That(binding(realm, "mania", 7, (int)ManiaAction.Key4, 0), Is.EqualTo(new KeyCombination(InputKey.Space)));
                    Assert.That(binding(realm, "mania", 5, (int)ManiaAction.Key1, 0), Is.EqualTo(new KeyCombination(InputKey.D)), "5K was not in the stable config");

                    // a second run has nothing left but the unticked dim level
                    var again = transfer.Preview(BanchosucksStableConfig.Parse(SAMPLE).Map(transfer.Capture(), out _), out _);
                    Assert.That(again.Select(c => c.Key), Is.EqualTo(new[] { nameof(OsuSetting.DimLevel) }));

                    // and the backup has the setup from before
                    var backup = transfer.ReadBackup()!;
                    Assert.That(BanchosucksSettingsDiff.SameValue(backup.Game[nameof(OsuSetting.AudioOffset)], "0"), Is.True);
                    Assert.That(backup.KeyBindings.Single(b => b.Ruleset == "osu" && b.Variant == 0 && b.Action == (int)OsuAction.LeftButton).Keys, Is.EqualTo(new[] { "Z", "MouseLeft" }));
                }
            });
        }

        #endregion

        #region Helpers

        private static readonly Ruleset[] rulesets = { new OsuRuleset(), new TaikoRuleset(), new CatchRuleset(), new ManiaRuleset() };

        /// <summary>
        /// The key bindings of a fresh installation, as a settings document.
        /// </summary>
        private static BanchosucksSettingsDocument defaultSetup()
        {
            var rows = new List<BanchosucksBindingRow>();

            foreach (var ruleset in rulesets)
            {
                foreach (int variant in ruleset.AllVariants)
                {
                    // the database keeps the slots of an action together, in the order of the defaults
                    foreach (var group in ruleset.GetDefaultKeyBindings(variant).GroupBy(b => (int)b.Action))
                        rows.AddRange(group.Select(b => new BanchosucksBindingRow(System.Guid.NewGuid(), ruleset.ShortName, variant, group.Key, b.KeyCombination.ToString())));
                }
            }

            return new BanchosucksSettingsDocument { KeyBindings = BanchosucksKeyBindings.FromRows(rows) };
        }

        private static BanchosucksSettingsDocument map(string stableText) => BanchosucksStableConfig.Parse(stableText).Map(defaultSetup(), out _);

        private static List<string> keysOf(BanchosucksSettingsDocument document, string ruleset, int variant, int action)
            => document.KeyBindings.Single(b => b.Ruleset == ruleset && b.Variant == variant && b.Action == action).Keys;

        private static KeyCombination binding(RealmAccess realm, string ruleset, int variant, int action, int slot)
            => realm.Run(r => r.All<RealmKeyBinding>().AsEnumerable().Where(b => b.RulesetName == ruleset && b.Variant == variant && b.ActionInt == action).ToList()[slot].KeyCombination);

        #endregion
    }
}

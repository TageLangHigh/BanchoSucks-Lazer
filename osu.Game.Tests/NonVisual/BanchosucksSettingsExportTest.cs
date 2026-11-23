// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Settings export / import (F46): what a file may contain, that it survives a round trip, and that a file
    /// from somewhere else cannot do anything the allow-list does not permit.
    /// </summary>
    [TestFixture]
    public partial class BanchosucksSettingsExportTest : RealmTest
    {
        private const string secret_username = "GeheimerSpielerName";
        private const string secret_token = "geheimes-token-0123456789abcdef";
        private const string secret_api_url = "https://geheim.example.invalid";
        private const string secret_discord_id = "987654321012345678";

        private static readonly OsuSetting[] account_settings =
        {
            OsuSetting.Username,
            OsuSetting.Token,
            OsuSetting.SavePassword,
            OsuSetting.SaveUsername,
            OsuSetting.CustomApiUrl,
            OsuSetting.BanchosucksDiscordAppId,
            OsuSetting.UserOnlineStatus,
            OsuSetting.WasSupporter,
        };

        #region Allow-list

        [Test]
        public void TestAllowListHasNoAccountSettings()
        {
            var exported = BanchosucksSettingsAllowList.ExportedGameSettings.ToList();

            foreach (var setting in account_settings.Concat(BanchosucksSettingsAllowList.NeverExported))
            {
                Assert.That(exported, Does.Not.Contain(setting), $"{setting} must never be exported");
                Assert.That(BanchosucksSettingsAllowList.IsGameSettingAllowed(setting.ToString()), Is.False, $"{setting} must never be imported");
            }

            Assert.That(exported, Is.Not.Empty);
            Assert.That(exported, Is.Unique);
        }

        [Test]
        public void TestFrameworkAllowListHasNothingAboutThePc()
        {
            string[] names = BanchosucksSettingsAllowList.FrameworkSettings.Select(s => s.ToString()).ToArray();

            // window position, display, resolution, renderer, audio device
            foreach (string forbidden in new[] { "Window", "Display", "Size", "Renderer", "AudioDevice", "Locale" })
                Assert.That(names.Any(n => n.Contains(forbidden, StringComparison.OrdinalIgnoreCase)), Is.False, forbidden);

            Assert.That(names, Does.Contain(nameof(FrameworkSetting.FrameSync)));
            Assert.That(names, Does.Contain(nameof(FrameworkSetting.VolumeUniversal)));
        }

        [Test]
        public void TestRulesetAllowListNamesRealSettings()
        {
            var enums = new Dictionary<string, Type>
            {
                ["osu"] = typeof(OsuRulesetSetting),
                ["taiko"] = typeof(TaikoRulesetSetting),
                ["mania"] = typeof(ManiaRulesetSetting),
            };

            Assert.That(BanchosucksSettingsAllowList.RulesetSettings.Keys, Is.EquivalentTo(enums.Keys));

            foreach (var (ruleset, keys) in BanchosucksSettingsAllowList.RulesetSettings)
            {
                foreach (string key in keys)
                    Assert.That(Enum.GetNames(enums[ruleset]), Does.Contain(key), $"{ruleset}: {key}");
            }
        }

        #endregion

        #region Privacy

        [Test]
        public void TestExportNeverContainsTheAccount()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using (var frameworkConfig = new FrameworkConfigManager(storage))
                using (var config = new OsuConfigManager(storage))
                {
                    registerBindings(realm);
                    fillAccount(config);

                    var transfer = new BanchosucksSettingsTransfer(config, frameworkConfig, realm, storage) { ClientVersion = "2026.913.17" };

                    string filename = transfer.Export();
                    string text = read(transfer.ExportStorage, filename);

                    assertNoAccount(text, storage);

                    // the backup an import writes is built the same way
                    transfer.WriteBackup();
                    assertNoAccount(read(storage.GetStorageForDirectory(BanchosucksKeyProfiles.DIRECTORY), BanchosucksSettingsTransfer.BACKUP_FILENAME), storage);

                    // and it is a real export, not an empty file
                    var root = JObject.Parse(text);
                    Assert.That(((JObject)root["game"]!).Count, Is.EqualTo(BanchosucksSettingsAllowList.ExportedGameSettings.Count()));
                    Assert.That(((JArray)root["keyBindings"]!).Count, Is.GreaterThan(50));
                    Assert.That((string?)root["format"], Is.EqualTo(BanchosucksSettingsDocument.FORMAT));
                    Assert.That((int)root["version"]!, Is.EqualTo(BanchosucksSettingsDocument.VERSION));
                }
            });
        }

        [Test]
        public void TestSerialiseDropsWhatIsNotAllowed()
        {
            // even a document somebody filled by hand cannot carry the account out
            var document = new BanchosucksSettingsDocument();
            document.Game[nameof(OsuSetting.Token)] = secret_token;
            document.Game[nameof(OsuSetting.Username)] = secret_username;
            document.Game[nameof(OsuSetting.CustomApiUrl)] = secret_api_url;
            document.Game[nameof(OsuSetting.DimLevel)] = "0.5";
            document.Framework["AudioDevice"] = "Lautsprecher (GeheimesGerät)";
            document.Framework["WindowedPositionX"] = "0.4";
            document.Mouse["Something"] = "1";
            document.RulesetSettings.Add(new BanchosucksRulesetSettingEntry { Ruleset = "osu", Key = "NotASetting", Value = secret_token });

            string text = document.Serialise();

            Assert.That(text, Does.Not.Contain(secret_token));
            Assert.That(text, Does.Not.Contain(secret_username));
            Assert.That(text, Does.Not.Contain(secret_api_url));
            Assert.That(text, Does.Not.Contain("GeheimesGerät"));
            Assert.That(text, Does.Not.Contain("WindowedPositionX"));
            Assert.That(text, Does.Contain("DimLevel"));
        }

        [Test]
        public void TestImportCannotTouchTheAccount()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using (var frameworkConfig = new FrameworkConfigManager(storage))
                using (var config = new OsuConfigManager(storage))
                {
                    fillAccount(config);
                    config.SetValue(OsuSetting.DimLevel, 0.7);

                    var document = BanchosucksSettingsDocument.Parse(@"{
                        ""format"": ""banchosucks-settings"", ""version"": 1,
                        ""game"": { ""Token"": ""fremd"", ""Username"": ""fremd"", ""SavePassword"": ""False"", ""SaveUsername"": ""False"",
                                    ""CustomApiUrl"": ""https://boese.example.invalid"", ""BanchosucksDiscordAppId"": ""1"", ""DimLevel"": ""0.3"" },
                        ""framework"": { ""AudioDevice"": ""fremd"", ""Renderer"": ""Vulkan"", ""WindowedPositionX"": ""0.9"" }
                    }");

                    Assert.That(document.Game.Keys, Is.EquivalentTo(new[] { nameof(OsuSetting.DimLevel) }));
                    Assert.That(document.Framework, Is.Empty);
                    Assert.That(document.Skipped, Has.Count.EqualTo(9));

                    var transfer = new BanchosucksSettingsTransfer(config, frameworkConfig, realm, storage);
                    transfer.Apply(document);

                    Assert.That(config.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.3).Within(0.001));
                    Assert.That(config.Get<string>(OsuSetting.Username), Is.EqualTo(secret_username));
                    Assert.That(config.Get<string>(OsuSetting.Token), Is.EqualTo(secret_token));
                    Assert.That(config.Get<string>(OsuSetting.CustomApiUrl), Is.EqualTo(secret_api_url));
                    Assert.That(config.Get<bool>(OsuSetting.SavePassword), Is.True);
                    Assert.That(config.Get<bool>(OsuSetting.SaveUsername), Is.True);

                    // a document that was filled around the parser does not get further either
                    var forged = new BanchosucksSettingsDocument();
                    forged.Game[nameof(OsuSetting.Token)] = "fremd";
                    forged.Game[nameof(OsuSetting.CustomApiUrl)] = "https://boese.example.invalid";

                    var result = transfer.Apply(forged);

                    Assert.That(result.Applied, Is.Zero);
                    Assert.That(config.Get<string>(OsuSetting.Token), Is.EqualTo(secret_token));
                    Assert.That(config.Get<string>(OsuSetting.CustomApiUrl), Is.EqualTo(secret_api_url));
                }
            });
        }

        private static void fillAccount(OsuConfigManager config)
        {
            config.SetValue(OsuSetting.Username, secret_username);
            config.SetValue(OsuSetting.Token, secret_token);
            config.SetValue(OsuSetting.CustomApiUrl, secret_api_url);
            config.SetValue(OsuSetting.BanchosucksDiscordAppId, secret_discord_id);
            config.SetValue(OsuSetting.SavePassword, true);
            config.SetValue(OsuSetting.SaveUsername, true);
        }

        private static void assertNoAccount(string text, Storage storage)
        {
            Assert.That(text, Does.Not.Contain(secret_username));
            Assert.That(text, Does.Not.Contain(secret_token));
            Assert.That(text, Does.Not.Contain(secret_api_url));
            Assert.That(text, Does.Not.Contain(secret_discord_id));

            var root = JObject.Parse(text);
            string[] gameKeys = ((JObject)root["game"]!).Properties().Select(p => p.Name).ToArray();

            foreach (var setting in account_settings.Concat(BanchosucksSettingsAllowList.NeverExported))
                Assert.That(gameKeys, Does.Not.Contain(setting.ToString()));

            // every key anywhere in the file: nothing that sounds like an account
            // ("NotifyOnUsernameMentioned" is a yes/no preference, not the name)
            string[] allKeys = root.Descendants().OfType<JProperty>().Select(p => p.Name)
                                   .Where(n => n != nameof(OsuSetting.NotifyOnUsernameMentioned))
                                   .Select(n => n.ToLowerInvariant()).Distinct().ToArray();

            foreach (string forbidden in new[] { "username", "token", "password", "email", "mail", "apiurl", "discordappid" })
                Assert.That(allKeys.Any(k => k.Contains(forbidden)), Is.False, forbidden);

            // nothing about the PC: neither the Windows user nor a path of the installation
            if (Environment.UserName.Length >= 4)
                Assert.That(text, Does.Not.Contain(Environment.UserName).IgnoreCase);

            Assert.That(text, Does.Not.Contain(storage.GetFullPath(string.Empty).TrimEnd('\\', '/')));
            Assert.That(text.Replace(@"\\", @"\"), Does.Not.Contain(storage.GetFullPath(string.Empty).TrimEnd('\\', '/')));
        }

        #endregion

        #region Round trip

        [Test]
        public void TestRoundTripRestoresTheSetup()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using (var frameworkConfig = new FrameworkConfigManager(storage))
                using (var config = new OsuConfigManager(storage))
                using (var osuSettings = (OsuRulesetConfigManager)new OsuRuleset().CreateConfig(new SettingsStore(realm)))
                {
                    registerBindings(realm);

                    var transfer = new BanchosucksSettingsTransfer(config, frameworkConfig, realm, storage)
                    {
                        RulesetSettingsLookup = name => name == "osu" ? osuSettings : null,
                    };

                    // the setup that gets exported
                    config.SetValue(OsuSetting.DimLevel, 0.25);
                    config.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Classic);
                    config.SetValue(OsuSetting.AudioOffset, -17.0);
                    frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Limit2x);
                    frameworkConfig.SetValue(FrameworkSetting.VolumeMusic, 0.4);
                    osuSettings.SetValue(OsuRulesetSetting.SnakingInSliders, false);
                    setBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 0, InputKey.A);
                    setBinding(realm, "mania", 4, (int)ManiaAction.Key1, 0, InputKey.Q);
                    setBinding(realm, null, null, (int)GlobalAction.ToggleChat, 0, InputKey.F9);

                    string text = transfer.Capture().Serialise();

                    // something else in between
                    config.SetValue(OsuSetting.DimLevel, 0.9);
                    config.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Standardised);
                    config.SetValue(OsuSetting.AudioOffset, 30.0);
                    frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Unlimited);
                    frameworkConfig.SetValue(FrameworkSetting.VolumeMusic, 1.0);
                    osuSettings.SetValue(OsuRulesetSetting.SnakingInSliders, true);
                    setBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 0, InputKey.Z);
                    setBinding(realm, "mania", 4, (int)ManiaAction.Key1, 0, InputKey.D);
                    setBinding(realm, null, null, (int)GlobalAction.ToggleChat, 0, InputKey.F8);

                    var incoming = BanchosucksSettingsDocument.Parse(text);
                    Assert.That(incoming.Skipped, Is.Empty);

                    var changes = transfer.Preview(incoming, out var notes);

                    Assert.That(notes, Is.Empty);
                    Assert.That(changes.Select(c => (c.Section, c.Key)), Is.EquivalentTo(new[]
                    {
                        (BanchosucksSettingsSection.Game, nameof(OsuSetting.DimLevel)),
                        (BanchosucksSettingsSection.Game, nameof(OsuSetting.ScoreDisplayMode)),
                        (BanchosucksSettingsSection.Game, nameof(OsuSetting.AudioOffset)),
                        (BanchosucksSettingsSection.Framework, nameof(FrameworkSetting.FrameSync)),
                        (BanchosucksSettingsSection.Framework, nameof(FrameworkSetting.VolumeMusic)),
                        (BanchosucksSettingsSection.RulesetSettings, nameof(OsuRulesetSetting.SnakingInSliders)),
                        (BanchosucksSettingsSection.KeyBindings, ((int)OsuAction.LeftButton).ToString()),
                        (BanchosucksSettingsSection.KeyBindings, ((int)ManiaAction.Key1).ToString()),
                        (BanchosucksSettingsSection.KeyBindings, ((int)GlobalAction.ToggleChat).ToString()),
                    }));

                    var dim = changes.Single(c => c.Key == nameof(OsuSetting.DimLevel));
                    Assert.That(dim.Old, Is.EqualTo("0.9"));
                    Assert.That(dim.New, Is.EqualTo("0.25"));

                    var result = transfer.Apply(incoming);

                    Assert.That(result.Skipped, Is.Zero);
                    Assert.That(config.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.25).Within(0.001));
                    Assert.That(config.Get<ScoringMode>(OsuSetting.ScoreDisplayMode), Is.EqualTo(ScoringMode.Classic));
                    Assert.That(config.Get<double>(OsuSetting.AudioOffset), Is.EqualTo(-17));
                    Assert.That(frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync), Is.EqualTo(FrameSync.Limit2x));
                    Assert.That(frameworkConfig.Get<double>(FrameworkSetting.VolumeMusic), Is.EqualTo(0.4).Within(0.001));
                    Assert.That(osuSettings.Get<bool>(OsuRulesetSetting.SnakingInSliders), Is.False);
                    Assert.That(getBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 0), Is.EqualTo(new KeyCombination(InputKey.A)));
                    Assert.That(getBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 1), Is.EqualTo(new KeyCombination(InputKey.MouseLeft)), "second slot untouched");
                    Assert.That(getBinding(realm, "mania", 4, (int)ManiaAction.Key1, 0), Is.EqualTo(new KeyCombination(InputKey.Q)));
                    Assert.That(getBinding(realm, "mania", 7, (int)ManiaAction.Key1, 0), Is.EqualTo(new KeyCombination(InputKey.S)), "other variant untouched");
                    Assert.That(getBinding(realm, null, null, (int)GlobalAction.ToggleChat, 0), Is.EqualTo(new KeyCombination(InputKey.F9)));

                    // nothing left to change afterwards
                    Assert.That(transfer.Preview(BanchosucksSettingsDocument.Parse(text), out _), Is.Empty);

                    // the backup holds the setup from before the import and brings it back
                    var backup = transfer.ReadBackup();

                    Assert.That(backup, Is.Not.Null);
                    Assert.That(backup!.Game[nameof(OsuSetting.DimLevel)], Is.EqualTo("0.9"));

                    transfer.Apply(backup);

                    Assert.That(config.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.9).Within(0.001));
                    Assert.That(frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync), Is.EqualTo(FrameSync.Unlimited));
                    Assert.That(osuSettings.Get<bool>(OsuRulesetSetting.SnakingInSliders), Is.True);
                    Assert.That(getBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 0), Is.EqualTo(new KeyCombination(InputKey.Z)));
                    Assert.That(getBinding(realm, null, null, (int)GlobalAction.ToggleChat, 0), Is.EqualTo(new KeyCombination(InputKey.F8)));
                }
            });
        }

        [Test]
        public void TestOnlyAcceptedChangesAreApplied()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using (var frameworkConfig = new FrameworkConfigManager(storage))
                using (var config = new OsuConfigManager(storage))
                {
                    registerBindings(realm);

                    var transfer = new BanchosucksSettingsTransfer(config, frameworkConfig, realm, storage);

                    config.SetValue(OsuSetting.DimLevel, 0.2);
                    config.SetValue(OsuSetting.KeyOverlay, true);
                    setBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 0, InputKey.A);
                    setBinding(realm, "taiko", 0, (int)TaikoAction.LeftRim, 0, InputKey.Q);

                    var incoming = BanchosucksSettingsDocument.Parse(transfer.Capture().Serialise());

                    config.SetValue(OsuSetting.DimLevel, 0.8);
                    config.SetValue(OsuSetting.KeyOverlay, false);
                    setBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 0, InputKey.Z);
                    setBinding(realm, "taiko", 0, (int)TaikoAction.LeftRim, 0, InputKey.D);

                    var changes = transfer.Preview(incoming, out _);
                    Assert.That(changes, Has.Count.EqualTo(4));

                    // the player unticks the dim level and the taiko keys
                    var accepted = changes.Where(c => c.Key != nameof(OsuSetting.DimLevel) && c.Ruleset != "taiko").ToList();
                    transfer.Apply(BanchosucksSettingsDiff.Restrict(incoming, accepted));

                    Assert.That(config.Get<double>(OsuSetting.DimLevel), Is.EqualTo(0.8).Within(0.001));
                    Assert.That(config.Get<bool>(OsuSetting.KeyOverlay), Is.True);
                    Assert.That(getBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 0), Is.EqualTo(new KeyCombination(InputKey.A)));
                    Assert.That(getBinding(realm, "taiko", 0, (int)TaikoAction.LeftRim, 0), Is.EqualTo(new KeyCombination(InputKey.D)));
                }
            });
        }

        #endregion

        #region Tolerance

        [Test]
        public void TestUnknownAndNewerEntriesAreSkipped()
        {
            RunTestWithRealm((realm, storage) =>
            {
                using (var frameworkConfig = new FrameworkConfigManager(storage))
                using (var config = new OsuConfigManager(storage))
                using (var osuSettings = (OsuRulesetConfigManager)new OsuRuleset().CreateConfig(new SettingsStore(realm)))
                {
                    registerBindings(realm);
                    config.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Standardised);
                    config.SetValue(OsuSetting.HUDVisibilityMode, HUDVisibilityMode.Always);
                    config.SetValue(OsuSetting.KeyOverlay, false);

                    var document = BanchosucksSettingsDocument.Parse(@"{
                        ""format"": ""banchosucks-settings"",
                        ""version"": 99,
                        ""created"": ""2031-05-06T07:08:09Z"",
                        ""somethingNew"": { ""a"": 1 },
                        ""game"": {
                            ""SettingFromTheFuture"": ""1"",
                            ""ScoreDisplayMode"": ""NotAMode"",
                            ""HUDVisibilityMode"": ""17"",
                            ""DimLevel"": ""NaN"",
                            ""BlurLevel"": ""viel"",
                            ""UIScale"": [1, 2],
                            ""KeyOverlay"": true
                        },
                        ""framework"": { ""FrameSync"": ""Limit9000x"", ""VolumeMusic"": ""0.5"" },
                        ""mouse"": { ""Sensitivity"": ""2"" },
                        ""skin"": { ""name"": ""Ein Skin den es hier nicht gibt"" },
                        ""keyBindings"": [
                            { ""ruleset"": ""osu"", ""variant"": 0, ""action"": 0, ""keys"": [""KeyFromTheFuture"", ""MouseLeft""] },
                            { ""ruleset"": ""osu"", ""variant"": 0, ""action"": 1, ""keys"": [""MouseWheelUp"", ""MouseRight""] },
                            { ""ruleset"": ""osu"", ""variant"": 0, ""action"": 4711, ""keys"": [""A""] },
                            { ""ruleset"": ""rulesetFromTheFuture"", ""variant"": 0, ""action"": 0, ""keys"": [""A""] },
                            { ""ruleset"": ""osu"", ""variant"": 0, ""action"": ""zero"", ""keys"": [""A""] },
                            ""nonsense""
                        ],
                        ""rulesetSettings"": [
                            { ""ruleset"": ""osu"", ""variant"": 0, ""key"": ""PlayfieldBorderStyle"", ""value"": ""99"" },
                            { ""ruleset"": ""osu"", ""variant"": 0, ""key"": ""SettingFromTheFuture"", ""value"": ""1"" },
                            { ""ruleset"": ""rulesetFromTheFuture"", ""variant"": 0, ""key"": ""X"", ""value"": ""1"" }
                        ],
                        ""keyProfiles"": ""soon""
                    }");

                    Assert.That(document.IsNewerVersion, Is.True);
                    Assert.That(document.Created, Is.EqualTo(new DateTime(2031, 5, 6, 7, 8, 9, DateTimeKind.Utc)));
                    Assert.That(document.Skipped, Does.Contain("somethingNew"));
                    Assert.That(document.Skipped, Does.Contain("game: SettingFromTheFuture"));
                    Assert.That(document.Skipped, Does.Contain("game: UIScale"));
                    Assert.That(document.KeyBindings, Has.Count.EqualTo(4));
                    Assert.That(document.RulesetSettings, Has.Count.EqualTo(1));

                    var transfer = new BanchosucksSettingsTransfer(config, frameworkConfig, realm, storage)
                    {
                        RulesetSettingsLookup = name => name == "osu" ? osuSettings : null,
                    };

                    var changes = transfer.Preview(document, out var notes);

                    Assert.That(notes.Select(n => n.Type), Is.EquivalentTo(new[]
                    {
                        BanchosucksTransferNoteType.SkinMissing,
                        BanchosucksTransferNoteType.NewerFile,
                        BanchosucksTransferNoteType.EntriesSkipped,
                    }));
                    Assert.That(document.Skin, Is.Null);

                    // unusable keys are not even offered as key changes
                    Assert.That(changes.Where(c => c.Section == BanchosucksSettingsSection.KeyBindings), Is.Empty);

                    double dimBefore = config.Get<double>(OsuSetting.DimLevel);
                    double blurBefore = config.Get<double>(OsuSetting.BlurLevel);
                    var frameSyncBefore = frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync);

                    BanchosucksTransferResult result = null!;
                    Assert.DoesNotThrow(() => result = transfer.Apply(document));

                    // what was understood is applied ...
                    Assert.That(config.Get<bool>(OsuSetting.KeyOverlay), Is.True);
                    Assert.That(frameworkConfig.Get<double>(FrameworkSetting.VolumeMusic), Is.EqualTo(0.5).Within(0.001));

                    // ... everything else stays as it was
                    Assert.That(result.Skipped, Is.GreaterThan(0));
                    Assert.That(config.Get<ScoringMode>(OsuSetting.ScoreDisplayMode), Is.EqualTo(ScoringMode.Standardised));
                    Assert.That(config.Get<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode), Is.EqualTo(HUDVisibilityMode.Always));
                    Assert.That(config.Get<double>(OsuSetting.DimLevel), Is.EqualTo(dimBefore));
                    Assert.That(config.Get<double>(OsuSetting.BlurLevel), Is.EqualTo(blurBefore));
                    Assert.That(frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync), Is.EqualTo(frameSyncBefore));
                    Assert.That(osuSettings.Get<PlayfieldBorderStyle>(OsuRulesetSetting.PlayfieldBorderStyle), Is.EqualTo(PlayfieldBorderStyle.None));
                    Assert.That(getBinding(realm, "osu", 0, (int)OsuAction.LeftButton, 0), Is.EqualTo(new KeyCombination(InputKey.Z)));
                    Assert.That(getBinding(realm, "osu", 0, (int)OsuAction.RightButton, 0), Is.EqualTo(new KeyCombination(InputKey.X)));
                }
            });
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("kein json")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase(@"{ ""format"": ""etwas-anderes"", ""version"": 1 }")]
        [TestCase(@"{ ""format"": ""banchosucks-settings"", ""version"": 0 }")]
        [TestCase(@"{ ""format"": 5 }")]
        public void TestOtherFilesAreRefused(string text)
        {
            Assert.Throws<FormatException>(() => BanchosucksSettingsDocument.Parse(text));
        }

        [Test]
        public void TestMinimalFileIsAccepted()
        {
            var document = BanchosucksSettingsDocument.Parse(@"{ ""format"": ""banchosucks-settings"" }");

            Assert.That(document.Version, Is.EqualTo(1));
            Assert.That(document.IsEmpty, Is.True);
            Assert.That(document.Skipped, Is.Empty);
        }

        #endregion

        #region Key bindings as text

        [Test]
        public void TestKeyTextRoundTripsForEveryKey()
        {
            foreach (var key in Enum.GetValues<InputKey>().Distinct())
            {
                string text = BanchosucksKeyBindings.ToText(new KeyCombination(key));

                Assert.That(BanchosucksKeyBindings.TryParse(text, out var parsed), Is.True, text);
                Assert.That(parsed, Is.EqualTo(new KeyCombination(key)), text);
            }
        }

        [Test]
        public void TestKeyTextOfCombinations()
        {
            var combination = new KeyCombination(InputKey.Control, InputKey.Shift, InputKey.S);
            string text = BanchosucksKeyBindings.ToText(combination);

            Assert.That(text.Split('+'), Is.EquivalentTo(new[] { "Control", "Shift", "S" }));
            Assert.That(BanchosucksKeyBindings.TryParse(text, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(combination));

            Assert.That(BanchosucksKeyBindings.ToText(new KeyCombination(InputKey.None)), Is.EqualTo("None"));
            Assert.That(BanchosucksKeyBindings.ToText(new KeyCombination(InputKey.Z).ToString()), Is.EqualTo("Z"));
            Assert.That(BanchosucksKeyBindings.TryParse("None", out var none), Is.True);
            Assert.That(none, Is.EqualTo(new KeyCombination(InputKey.None)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("90")]
        [TestCase("-1")]
        [TestCase("z")]
        [TestCase("Z+")]
        [TestCase("Z+KeyFromTheFuture")]
        [TestCase("Z,X")]
        public void TestUnreadableKeyTexts(string? text)
        {
            Assert.That(BanchosucksKeyBindings.TryParse(text, out _), Is.False);
        }

        [Test]
        public void TestPlanRewritesOnlyWhatDiffers()
        {
            Guid left1 = Guid.NewGuid(), left2 = Guid.NewGuid(), right1 = Guid.NewGuid(), right2 = Guid.NewGuid(), smoke = Guid.NewGuid(), global = Guid.NewGuid();

            var rows = new[]
            {
                row(left1, "osu", 0, 0, InputKey.Z),
                row(right1, "osu", 0, 1, InputKey.X),
                row(smoke, "osu", 0, 2, InputKey.C),
                row(left2, "osu", 0, 0, InputKey.MouseLeft),
                row(right2, "osu", 0, 1, InputKey.MouseRight),
                row(global, string.Empty, 0, 5, InputKey.F8),
            };

            var incoming = new[]
            {
                // first slot changes, second stays
                entry("osu", 0, 0, "A", "MouseLeft"),
                // fewer slots than the database: the rest is cleared
                entry("osu", 0, 1, "S"),
                // more slots than the database: the rest is dropped; an unreadable key keeps the old one
                entry("osu", 0, 2, "KeyFromTheFuture", "V"),
                // an action the database does not have
                entry("osu", 0, 99, "B"),
                // another variant
                entry("osu", 5, 0, "B"),
                // mouse wheel is fine for global actions
                entry(string.Empty, 0, 5, "MouseWheelUp"),
            };

            var plan = BanchosucksKeyBindings.Plan(rows, incoming, out int skipped);

            Assert.That(skipped, Is.EqualTo(2));
            Assert.That(plan, Is.EquivalentTo(new[]
            {
                (left1, new KeyCombination(InputKey.A).ToString()),
                (right1, new KeyCombination(InputKey.S).ToString()),
                (right2, new KeyCombination(InputKey.None).ToString()),
                (global, new KeyCombination(InputKey.MouseWheelUp).ToString()),
            }));
        }

        [Test]
        public void TestGameplayBindingsRefuseTheMouseWheel()
        {
            var current = entry("osu", 0, 0, "Z", "MouseLeft");

            Assert.That(BanchosucksKeyBindings.Merge(current, entry("osu", 0, 0, "MouseWheelDown", "MouseWheelUp")), Is.EqualTo(new[] { "Z", "MouseLeft" }));
            Assert.That(BanchosucksKeyBindings.Merge(entry(string.Empty, 0, 0, "Z"), entry(string.Empty, 0, 0, "MouseWheelDown")), Is.EqualTo(new[] { "MouseWheelDown" }));
        }

        #endregion

        #region Values

        [Test]
        public void TestValuesFromFilesAreChecked()
        {
            var mode = new Bindable<ScoringMode>(ScoringMode.Standardised);

            Assert.That(BanchosucksSettingsText.TrySet(mode, "Classic"), Is.True);
            Assert.That(mode.Value, Is.EqualTo(ScoringMode.Classic));

            Assert.That(BanchosucksSettingsText.TrySet(mode, "1"), Is.False, "numbers are not enum names");
            Assert.That(BanchosucksSettingsText.TrySet(mode, "classic"), Is.False);
            Assert.That(BanchosucksSettingsText.TrySet(mode, ""), Is.False);
            Assert.That(BanchosucksSettingsText.TrySet(mode, null), Is.False);
            Assert.That(mode.Value, Is.EqualTo(ScoringMode.Classic));

            var level = new BindableDouble(0.5) { MinValue = 0, MaxValue = 1 };

            Assert.That(BanchosucksSettingsText.TrySet(level, "0.25"), Is.True);
            Assert.That(level.Value, Is.EqualTo(0.25));
            Assert.That(BanchosucksSettingsText.TrySet(level, "7"), Is.True, "out of range is clamped by the setting itself");
            Assert.That(level.Value, Is.EqualTo(1));
            Assert.That(BanchosucksSettingsText.TrySet(level, "NaN"), Is.False);
            Assert.That(BanchosucksSettingsText.TrySet(level, "Infinity"), Is.False);
            Assert.That(BanchosucksSettingsText.TrySet(level, "0,5"), Is.False, "decimal comma");
            Assert.That(level.Value, Is.EqualTo(1));

            var flag = new BindableBool();

            Assert.That(BanchosucksSettingsText.TrySet(flag, "True"), Is.True);
            Assert.That(flag.Value, Is.True);
            Assert.That(BanchosucksSettingsText.TrySet(flag, "ja"), Is.False);

            var disabled = new BindableBool { Disabled = true };
            Assert.That(BanchosucksSettingsText.TrySet(disabled, "True"), Is.False);

            Assert.That(BanchosucksSettingsText.TrySet(new Bindable<DateTime?>(), "2026-01-01"), Is.False, "types an import has no business with");
        }

        [Test]
        public void TestSameValue()
        {
            Assert.That(BanchosucksSettingsDiff.SameValue("0.7", "0.70"), Is.True);
            Assert.That(BanchosucksSettingsDiff.SameValue("1", "1.0"), Is.True);
            Assert.That(BanchosucksSettingsDiff.SameValue("True", "true"), Is.True);
            Assert.That(BanchosucksSettingsDiff.SameValue("Classic", "Classic"), Is.True);
            Assert.That(BanchosucksSettingsDiff.SameValue("0.7", "0.71"), Is.False);
            Assert.That(BanchosucksSettingsDiff.SameValue("Classic", "classic"), Is.False);
        }

        [Test]
        public void TestSkinIsFoundByFolderName()
        {
            string[] imported = { "osu! \"argon\" (2022)", "Cool Skin", "Rafis HDDT [rafis-2017]", "Anderer Skin [cool skin]" };

            Assert.That(BanchosucksSettingsTransfer.MatchSkinName("Cool Skin", imported), Is.EqualTo("Cool Skin"));
            Assert.That(BanchosucksSettingsTransfer.MatchSkinName("cool skin", imported), Is.EqualTo("Cool Skin"), "exact name before folder suffix");
            Assert.That(BanchosucksSettingsTransfer.MatchSkinName("rafis-2017", imported), Is.EqualTo("Rafis HDDT [rafis-2017]"));
            Assert.That(BanchosucksSettingsTransfer.MatchSkinName("Rafis", imported), Is.Null);
            Assert.That(BanchosucksSettingsTransfer.MatchSkinName("", imported), Is.Null);
            Assert.That(BanchosucksSettingsTransfer.MatchSkinName(null, imported), Is.Null);
        }

        #endregion

        #region Helpers

        private static readonly Ruleset[] rulesets = { new OsuRuleset(), new TaikoRuleset(), new CatchRuleset(), new ManiaRuleset() };

        private static void registerBindings(RealmAccess realm)
            => new RealmKeyBindingStore(realm, new ReadableKeyCombinationProvider()).Register(new GlobalActionContainer(null), rulesets.Select(r => r.RulesetInfo));

        private static void setBinding(RealmAccess realm, string? ruleset, int? variant, int action, int slot, InputKey key)
            => realm.Write(r => bindingsOf(r, ruleset, variant, action)[slot].KeyCombination = new KeyCombination(key));

        private static KeyCombination getBinding(RealmAccess realm, string? ruleset, int? variant, int action, int slot)
            => realm.Run(r => bindingsOf(r, ruleset, variant, action)[slot].KeyCombination);

        private static List<RealmKeyBinding> bindingsOf(Realms.Realm realm, string? ruleset, int? variant, int action)
            => realm.All<RealmKeyBinding>().AsEnumerable().Where(b => b.RulesetName == ruleset && b.Variant == variant && b.ActionInt == action).ToList();

        private static string read(Storage storage, string filename)
        {
            using (var stream = storage.GetStream(filename))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        private static BanchosucksBindingRow row(Guid id, string ruleset, int variant, int action, InputKey key)
            => new BanchosucksBindingRow(id, ruleset, variant, action, new KeyCombination(key).ToString());

        private static BanchosucksKeyBindingEntry entry(string ruleset, int variant, int action, params string[] keys)
            => new BanchosucksKeyBindingEntry { Ruleset = ruleset, Variant = variant, Action = action, Keys = keys.ToList() };

        #endregion
    }
}

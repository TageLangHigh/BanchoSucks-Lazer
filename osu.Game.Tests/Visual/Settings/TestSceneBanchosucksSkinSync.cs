// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// Skin sync from osu!stable (F43) against a temporary "Skins" folder and the real skin importer.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBanchosucksSkinSync : OsuTestScene
    {
        [Cached(typeof(ILocalUserPlayInfo))]
        private readonly TestPlayInfo playInfo = new TestPlayInfo();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private readonly Container content;

        private TemporaryNativeStorage stableFolder = null!;
        private BanchosucksSkinSync sync = null!;

        /// <summary>
        /// Folder names are unique per test, the imported skins stay in the test game's database.
        /// </summary>
        private string prefix = string.Empty;

        private string skinsDirectory => Path.Combine(stableFolder.GetFullPath(string.Empty), "Skins");

        public TestSceneBanchosucksSkinSync()
        {
            Child = content = new Container { RelativeSizeAxes = Axes.Both };
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setting off, not playing, empty record", () =>
            {
                config.SetValue(OsuSetting.BanchosucksSkinSync, false);
                playInfo.State.Value = LocalUserPlayingState.NotPlaying;
                storage.GetStorageForDirectory(BanchosucksKeyProfiles.DIRECTORY).Delete(BanchosucksSkinSyncRecord.FILENAME);
            });

            AddStep("stable folder with two skins", () =>
            {
                prefix = Guid.NewGuid().ToString("N").Substring(0, 8);
                stableFolder = new TemporaryNativeStorage($"stable-{prefix}");
                Directory.CreateDirectory(skinsDirectory);

                writeSkin("A");
                writeSkin("B");
            });

            createSync(BanchosucksSkinSync.FIRST_CHECK_DELAY);
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddUntilStep("sync idle", () => !sync.IsChecking);
            AddStep("remove sync and stable folder", () =>
            {
                config.SetValue(OsuSetting.BanchosucksSkinSync, false);
                content.Clear();
                stableFolder.Dispose();
            });
        }

        [Test]
        public void TestImportsNewAndChangedFoldersOnly()
        {
            Dictionary<string, (long, DateTime)> stableBefore = null!;

            AddStep("remember the stable folder", () => stableBefore = snapshot());

            check();
            AddAssert("both skins imported", () => skinsNamed("A").Count == 1 && skinsNamed("B").Count == 1);
            AddAssert("both folders recorded", () => BanchosucksSkinSyncRecord.Load(storage).Folders.Keys, () => Is.EquivalentTo(new[] { $"{prefix}-A", $"{prefix}-B" }));
            AddAssert("stable folder untouched", () => snapshot(), () => Is.EqualTo(stableBefore));

            Guid firstA = Guid.Empty;
            Guid firstB = Guid.Empty;
            int skinCount = 0;

            AddStep("remember", () =>
            {
                firstA = skinsNamed("A").Single();
                firstB = skinsNamed("B").Single();
                skinCount = allSkinCount();
            });

            check();
            AddAssert("nothing imported again", () => allSkinCount() == skinCount && skinsNamed("A").Single() == firstA && skinsNamed("B").Single() == firstB);

            AddStep("select skin A", () => skins.CurrentSkinInfo.Value = skins.Query(s => s.ID == firstA));
            AddStep("change an image of A in stable", () => File.WriteAllBytes(Path.Combine(skinsDirectory, $"{prefix}-A", "cursor.png"), new byte[777]));

            check();
            AddAssert("A imported anew, B left alone", () => skinsNamed("A").Single() != firstA && skinsNamed("B").Single() == firstB);
            AddUntilStep("the selection follows the new A", () => skins.CurrentSkinInfo.Value.ID == skinsNamed("A").Single());

            AddStep("a third skin appears, B disappears from stable", () =>
            {
                writeSkin("C");
                Directory.Delete(Path.Combine(skinsDirectory, $"{prefix}-B"), true);
            });

            check();
            AddAssert("C imported", () => skinsNamed("C").Count == 1);
            AddAssert("B is still in the game", () => skinsNamed("B").Single() == firstB);

            AddStep("back to the default skin", () => skins.CurrentSkinInfo.SetDefault());
        }

        [Test]
        public void TestNothingHappensDuringGameplay()
        {
            AddStep("playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);
            AddStep("check now", () => sync.CheckNow(true));
            AddAssert("no check started", () => !sync.IsChecking && sync.LastCheck.IsCompleted);
            AddAssert("nothing imported", () => skinsNamed("A").Count == 0);

            AddStep("in a break", () => playInfo.State.Value = LocalUserPlayingState.Break);
            AddStep("check now", () => sync.CheckNow(true));
            AddAssert("still nothing imported", () => !sync.IsChecking && skinsNamed("A").Count == 0);

            AddStep("back in the menu", () => playInfo.State.Value = LocalUserPlayingState.NotPlaying);
            check();
            AddAssert("now imported", () => skinsNamed("A").Count == 1);
        }

        [Test]
        public void TestAutomaticCheckOnlyWhileSwitchedOn()
        {
            createSync(100);

            AddWaitStep("wait", 10);
            AddAssert("off: nothing imported", () => skinsNamed("A").Count == 0 && !sync.IsChecking);

            AddStep("playing", () => playInfo.State.Value = LocalUserPlayingState.Playing);
            AddStep("switch the setting on", () => config.SetValue(OsuSetting.BanchosucksSkinSync, true));
            AddWaitStep("wait", 10);
            AddAssert("on, but playing: nothing imported", () => skinsNamed("A").Count == 0 && !sync.IsChecking);

            AddStep("back in the menu", () => playInfo.State.Value = LocalUserPlayingState.NotPlaying);
            AddUntilStep("imported without being asked", () => skinsNamed("A").Count == 1 && skinsNamed("B").Count == 1);
        }

        [Test]
        public void TestNoStableFolder()
        {
            AddStep("no Skins folder", () => Directory.Delete(skinsDirectory, true));
            AddStep("check now", () => sync.CheckNow(true));
            AddAssert("no check started", () => !sync.IsChecking && sync.LastCheck.IsCompleted);
            AddAssert("nothing was created in stable", () => !Directory.Exists(skinsDirectory));
        }

        private void createSync(double firstCheckDelay)
        {
            AddStep("create sync", () => content.Child = sync = new BanchosucksSkinSync
            {
                SkinsDirectoryOverride = skinsDirectory,
                FirstCheckDelay = firstCheckDelay,
            });
            AddUntilStep("wait for load", () => sync.IsLoaded);
        }

        private void check()
        {
            AddStep("check now", () => sync.CheckNow(true));
            AddUntilStep("check finished", () => !sync.IsChecking && sync.LastCheck.IsCompleted);
        }

        private void writeSkin(string name)
        {
            string directory = Path.Combine(skinsDirectory, $"{prefix}-{name}");

            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "skin.ini"), $"[General]\nName: {prefix}-{name}\nAuthor: Test\nVersion: 2.5\n");
            File.WriteAllBytes(Path.Combine(directory, "cursor.png"), new byte[512]);
        }

        /// <summary>
        /// IDs of the skins in the game (not on their way out) that came from a test folder.
        /// </summary>
        private List<Guid> skinsNamed(string name)
            => realm.Run(r => r.All<SkinInfo>().Where(s => !s.DeletePending).AsEnumerable().Where(s => s.Name == $"{prefix}-{name}").Select(s => s.ID).ToList());

        private int allSkinCount() => realm.Run(r => r.All<SkinInfo>().Count());

        /// <summary>
        /// Size and write time of every file below the stable folder.
        /// </summary>
        private Dictionary<string, (long, DateTime)> snapshot()
            => new DirectoryInfo(stableFolder.GetFullPath(string.Empty)).EnumerateFiles("*", SearchOption.AllDirectories)
                                                                        .ToDictionary(f => f.FullName, f => (f.Length, f.LastWriteTimeUtc));

        private class TestPlayInfo : ILocalUserPlayInfo
        {
            public readonly Bindable<LocalUserPlayingState> State = new Bindable<LocalUserPlayingState>();

            IBindable<LocalUserPlayingState> ILocalUserPlayInfo.PlayingState => State;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.General;
using osu.Game.Tests.NonVisual;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// The "Von osu!stable übernehmen" subsection (F44): preview with one checkbox per setting, confirmation, and the
    /// messages when there is nothing to read. The stable folder is a temporary one.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBanchosucksStableSettings : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Cached(typeof(IDialogOverlay))]
        private readonly DialogOverlay dialogOverlay = new DialogOverlay();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private readonly PopoverContainer content;

        private BanchosucksStableSettings subsection = null!;
        private TemporaryNativeStorage? stableFolder;

        public TestSceneBanchosucksStableSettings()
        {
            Children = new Drawable[]
            {
                content = new PopoverContainer { RelativeSizeAxes = Axes.Both },
                dialogOverlay,
            };
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("lazer defaults", () =>
            {
                config.SetValue(OsuSetting.AudioOffset, 0.0);
                config.SetValue(OsuSetting.DimLevel, 0.7);
                config.SetValue(OsuSetting.GameplayCursorSize, 1f);
                config.SetValue(OsuSetting.AutoCursorSize, false);
            });

            AddStep("create subsection", () =>
            {
                content.Child = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 420,
                    Child = subsection = new BanchosucksStableSettings(),
                };
            });

            AddUntilStep("wait for load", () => subsection.IsLoaded);
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("remove stable folder", () =>
            {
                stableFolder?.Dispose();
                stableFolder = null;
            });
        }

        [Test]
        public void TestPreviewPerSettingAndApply()
        {
            AddStep("read sample config", () => subsection.ReadConfigText(BanchosucksStableConfigTest.SAMPLE));
            AddUntilStep("preview shown", () => subsection.CurrentPreview?.IsLoaded == true);

            AddAssert("offset has its own line with old and new value", () =>
            {
                var group = subsection.CurrentPreview!.Groups.Single(g => g.Changes.Any(c => c.Key == nameof(OsuSetting.AudioOffset)));
                return group.Changes.Count == 1 && group.Detail == "0 → -12";
            });
            AddAssert("dim has its own line", () => subsection.CurrentPreview!.Groups.Single(g => g.Changes.Any(c => c.Key == nameof(OsuSetting.DimLevel))).Detail == "0.7 → 0.3");
            AddAssert("keys are grouped per mode", () =>
                subsection.CurrentPreview!.Groups.Where(g => g.Changes.All(c => c.Section == BanchosucksSettingsSection.KeyBindings))
                          .Select(g => g.Changes.Select(c => c.Ruleset).Distinct().Single()), () => Is.EquivalentTo(new[] { "osu", "taiko", "fruits", "mania" }));
            AddAssert("nothing applied yet", () => config.Get<double>(OsuSetting.AudioOffset), () => Is.EqualTo(0));

            AddStep("untick the dim level", () => subsection.CurrentPreview!.Groups.Single(g => g.Changes.Any(c => c.Key == nameof(OsuSetting.DimLevel))).Ticked.Value = false);
            AddStep("apply ticked", () => subsection.CurrentPreview!.Accept());
            AddUntilStep("asks first", () => dialogOverlay.CurrentDialog != null);
            AddAssert("still nothing applied", () => config.Get<double>(OsuSetting.AudioOffset), () => Is.EqualTo(0));

            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());
            AddUntilStep("applied", () => subsection.AppliedCount == 1);
            AddAssert("offset taken over with turned sign", () => config.Get<double>(OsuSetting.AudioOffset), () => Is.EqualTo(-12));
            AddAssert("cursor size taken over", () => config.Get<float>(OsuSetting.GameplayCursorSize), () => Is.EqualTo(1.25f).Within(0.001f));
            AddAssert("dim level left alone", () => config.Get<double>(OsuSetting.DimLevel), () => Is.EqualTo(0.7).Within(0.001));
        }

        [Test]
        public void TestReadsTheUsersConfigFromTheFolder()
        {
            AddStep("stable folder with a config", () =>
            {
                stableFolder = new TemporaryNativeStorage($"stable-{Guid.NewGuid()}");
                File.WriteAllText(Path.Combine(stableFolder.GetFullPath(string.Empty), $"osu!.{Environment.UserName}.cfg"), BanchosucksStableConfigTest.SAMPLE);
                File.WriteAllText(Path.Combine(stableFolder.GetFullPath(string.Empty), "osu!.cfg"), "Offset = 99\n");
            });

            AddStep("read the folder", () => subsection.ReadFrom(stableFolder!));
            AddUntilStep("preview shown", () => subsection.CurrentPreview?.IsLoaded == true);
            AddAssert("offset from the user's file", () => subsection.CurrentPreview!.Groups.SelectMany(g => g.Changes).Single(c => c.Key == nameof(OsuSetting.AudioOffset)).New == "-12");
            AddAssert("nothing was written to the stable folder", () => Directory.GetFileSystemEntries(stableFolder!.GetFullPath(string.Empty)).Length == 2);

            AddStep("cancel", () => subsection.CurrentPreview!.Cancelled?.Invoke());
            AddAssert("preview closed", () => subsection.CurrentPreview == null);
            AddAssert("nothing applied", () => config.Get<double>(OsuSetting.AudioOffset), () => Is.EqualTo(0));
        }

        [Test]
        public void TestNothingToRead()
        {
            AddStep("empty stable folder", () => stableFolder = new TemporaryNativeStorage($"stable-{Guid.NewGuid()}"));
            AddStep("read the folder", () => subsection.ReadFrom(stableFolder!));
            AddAssert("no preview", () => subsection.CurrentPreview == null);

            AddStep("a config without any of the settings", () => subsection.ReadConfigText("# osu! configuration\nLanguage = de\nUsername = Jemand\n"));
            AddAssert("no preview", () => subsection.CurrentPreview == null);

            // the test game has no stable installation and no way to locate one
            AddStep("no installation", () => subsection.ReadStable());
            AddAssert("no preview, no dialog", () => subsection.CurrentPreview == null && dialogOverlay.CurrentDialog == null);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

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
using osu.Game.IO;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.General;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// The "Einstellungen sichern" subsection (F46): export, import with preview and confirmation, restore of the backup.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBanchosucksSettingsBackup : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Cached(typeof(IDialogOverlay))]
        private readonly DialogOverlay dialogOverlay = new DialogOverlay();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private BanchosucksSettingsBackupSettings subsection = null!;

        private readonly PopoverContainer content;

        public TestSceneBanchosucksSettingsBackup()
        {
            Children = new Drawable[]
            {
                content = new PopoverContainer { RelativeSizeAxes = Axes.Both },
                dialogOverlay,
            };
        }

        private Storage exportStorage => (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create subsection", () =>
            {
                content.Child = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 420,
                    Child = subsection = new BanchosucksSettingsBackupSettings { RevealExportedFile = false },
                };
            });

            AddUntilStep("wait for load", () => subsection.IsLoaded);
        }

        [Test]
        public void TestExportImportAndRestore()
        {
            AddStep("dim 25 %", () => config.SetValue(OsuSetting.DimLevel, 0.25));
            AddStep("export", () => subsection.Export());
            AddAssert("file written", () => subsection.LastExportedFile != null && exportStorage.Exists(subsection.LastExportedFile));
            AddAssert("file has no account", () =>
            {
                using (var reader = new StreamReader(exportStorage.GetStream(subsection.LastExportedFile)))
                {
                    string text = reader.ReadToEnd();
                    return !text.Contains(nameof(OsuSetting.Token)) && !text.Contains("\"Username\"") && text.Contains(nameof(OsuSetting.DimLevel));
                }
            });

            AddStep("dim 90 %", () => config.SetValue(OsuSetting.DimLevel, 0.9));
            AddStep("import the file", () => subsection.ImportFile(exportStorage.GetFullPath(subsection.LastExportedFile!)));

            AddUntilStep("preview shown", () => subsection.CurrentPreview?.IsLoaded == true);
            AddAssert("one group, dim level", () => subsection.CurrentPreview!.Groups.Count == 1
                                                    && subsection.CurrentPreview.Groups[0].Changes.Single().Key == nameof(OsuSetting.DimLevel));
            AddAssert("nothing applied yet", () => config.Get<double>(OsuSetting.DimLevel), () => Is.EqualTo(0.9).Within(0.001));

            AddStep("apply ticked", () => subsection.CurrentPreview!.Accept());
            AddUntilStep("asks first", () => dialogOverlay.CurrentDialog != null);
            AddAssert("still nothing applied", () => config.Get<double>(OsuSetting.DimLevel), () => Is.EqualTo(0.9).Within(0.001));

            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());
            AddUntilStep("applied", () => subsection.AppliedCount == 1);
            AddAssert("dim back at 25 %", () => config.Get<double>(OsuSetting.DimLevel), () => Is.EqualTo(0.25).Within(0.001));
            AddAssert("preview closed", () => subsection.CurrentPreview == null);

            AddUntilStep("dialog gone", () => dialogOverlay.CurrentDialog == null);
            AddStep("restore backup", () => subsection.RestoreBackup());
            AddUntilStep("preview shown", () => subsection.CurrentPreview?.IsLoaded == true);
            AddStep("apply ticked", () => subsection.CurrentPreview!.Accept());
            AddUntilStep("asks first", () => dialogOverlay.CurrentDialog != null);
            AddStep("confirm", () => dialogOverlay.CurrentDialog!.PerformOkAction());
            AddUntilStep("applied", () => subsection.AppliedCount == 2);
            AddAssert("dim at 90 % again", () => config.Get<double>(OsuSetting.DimLevel), () => Is.EqualTo(0.9).Within(0.001));
        }

        [Test]
        public void TestUntickedGroupIsNotApplied()
        {
            AddStep("setup A", () =>
            {
                config.SetValue(OsuSetting.DimLevel, 0.1);
                config.SetValue(OsuSetting.KeyOverlay, true);
            });
            AddStep("export", () => subsection.Export());
            AddStep("setup B", () =>
            {
                config.SetValue(OsuSetting.DimLevel, 0.6);
                config.SetValue(OsuSetting.KeyOverlay, false);
            });

            AddStep("import the file", () => subsection.ImportFile(exportStorage.GetFullPath(subsection.LastExportedFile!)));
            AddUntilStep("preview shown", () => subsection.CurrentPreview?.IsLoaded == true);
            AddAssert("two changes in one group", () => subsection.CurrentPreview!.Groups.Single().Changes.Count == 2);

            AddStep("untick the group", () => subsection.CurrentPreview!.Groups.Single().Ticked.Value = false);
            AddStep("apply ticked", () => subsection.CurrentPreview!.Accept());

            AddAssert("no question, preview closed", () => dialogOverlay.CurrentDialog == null && subsection.CurrentPreview == null);
            AddAssert("nothing changed", () => config.Get<double>(OsuSetting.DimLevel), () => Is.EqualTo(0.6).Within(0.001));
        }

        [Test]
        public void TestCancelAndForeignFile()
        {
            string foreignFile = string.Empty;

            AddStep("write a foreign json", () =>
            {
                using (var writer = new StreamWriter(exportStorage.CreateFileSafely("etwas-anderes.json")))
                    writer.Write("{ \"hello\": \"world\" }");

                foreignFile = exportStorage.GetFullPath("etwas-anderes.json");
            });

            AddStep("import it", () => subsection.ImportFile(foreignFile));
            AddAssert("refused, no preview", () => subsection.CurrentPreview == null);

            AddStep("dim 30 %", () => config.SetValue(OsuSetting.DimLevel, 0.3));
            AddStep("export", () => subsection.Export());
            AddStep("dim 70 %", () => config.SetValue(OsuSetting.DimLevel, 0.7));
            AddStep("import the file", () => subsection.ImportFile(exportStorage.GetFullPath(subsection.LastExportedFile!)));
            AddUntilStep("preview shown", () => subsection.CurrentPreview?.IsLoaded == true);

            AddStep("cancel", () => subsection.CurrentPreview!.Cancelled?.Invoke());
            AddAssert("preview closed", () => subsection.CurrentPreview == null);
            AddAssert("nothing changed", () => config.Get<double>(OsuSetting.DimLevel), () => Is.EqualTo(0.7).Within(0.001));
        }

        [Test]
        public void TestImportOfTheCurrentSetupHasNothingToChange()
        {
            AddStep("export", () => subsection.Export());
            AddStep("import the file", () => subsection.ImportFile(exportStorage.GetFullPath(subsection.LastExportedFile!)));
            AddUntilStep("preview shown", () => subsection.CurrentPreview?.IsLoaded == true);
            AddAssert("no groups", () => subsection.CurrentPreview!.Groups.Count == 0);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// Banchosucks: the pp preview line in song select's difficulty wedge, with a real beatmap and the real calculators.
    /// </summary>
    public partial class TestSceneBanchosucksPerformancePreview : SongSelectTestScene
    {
        private BanchosucksPerformancePreviewLine line => SongSelect.ChildrenOfType<BanchosucksPerformancePreviewLine>().Single();

        private double?[] noModValues = null!;

        [Test]
        public void TestPreviewFollowsSelectionModsAndRuleset()
        {
            AddStep("import actual beatmap", () => Beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely());
            AddStep("show converts", () => Config.SetValue(OsuSetting.ShowConvertedBeatmaps, true));

            LoadSongSelect();

            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);
            waitForValues();

            AddAssert("three values", () => line.DisplayedValues, () => Has.Count.EqualTo(3).And.All.Not.Null);
            AddAssert("pp rises with accuracy", () => line.DisplayedValues[0] < line.DisplayedValues[1] && line.DisplayedValues[1] < line.DisplayedValues[2]);
            AddAssert("numbers are shown", () => valueTexts(), () => Has.All.Match(@"^\d+$"));
            AddAssert("tooltip names the rule", () => line.TooltipText.ToString(), () => Does.Contain("GREAT and OK"));
            AddStep("remember values", () => noModValues = line.DisplayedValues.ToArray());

            ChangeMods(new OsuModDoubleTime());
            AddUntilStep("values changed with the mod", () => !line.Calculating && line.DisplayedValues[2] > noModValues[2]);

            ChangeMods();
            AddUntilStep("values back", () => !line.Calculating && line.DisplayedValues.SequenceEqual(noModValues));

            // a convert gets no pp in osu!mania on this server: dashes, not numbers.
            ChangeRuleset(3);
            AddUntilStep("mania selected", () => Beatmap.Value.BeatmapInfo.Ruleset.OnlineID == 0 && Ruleset.Value.OnlineID == 3);
            AddUntilStep("dashes for the convert", () => !line.Calculating && line.DisplayedValues.All(v => v == null));
            AddAssert("dashes are shown", () => valueTexts(), () => Has.All.EqualTo("–"));
            AddAssert("tooltip names the mania rule", () => line.TooltipText.ToString(), () => Does.Contain("PERFECT per GREAT"));

            ChangeMods(new ManiaModRandom());
            AddUntilStep("still dashes", () => !line.Calculating && line.DisplayedValues.All(v => v == null));

            ChangeMods();
            ChangeRuleset(0);
            AddUntilStep("values back", () => !line.Calculating && line.DisplayedValues.SequenceEqual(noModValues));
        }

        private void waitForValues() => AddUntilStep("wait for calculation", () => !line.Calculating && line.DisplayedValues.Any(v => v != null));

        private string[] valueTexts() => line.ChildrenOfType<OsuSpriteText>()
                                             .Select(t => t.Text.ToString())
                                             .Where(t => t != "pp" && !t.EndsWith('%'))
                                             .ToArray();
    }
}

// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Online;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneLegacySongSelect : SongSelectTestScene
    {
        [Cached]
        private readonly LocalUserStatisticsProvider statisticsProvider = new LocalUserStatisticsProvider();

        [Test]
        public void TestLegacyLayout()
        {
            AddStep("load user statistics", () => Add(statisticsProvider));
            AddStep("enable legacy song select", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            ImportBeatmapForRuleset(0);
            LoadSongSelect();
            AddUntilStep("legacy top loaded", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("global footer owns legacy presentation", () => SongSelect.FooterContent.Value.Mode == ScreenFooterContent.FooterMode.Argon && SongSelect.AllowLegacyFooterSkinning);
            AddUntilStep("modern filter hidden", () => !SongSelect.ChildrenOfType<FilterControl>().Single().IsPresent);
            AddStep("disable legacy live", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, false));
            AddUntilStep("modern filter restored", () => SongSelect.ChildrenOfType<FilterControl>().Single().Alpha == 1);
            AddStep("enable legacy live", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            AddUntilStep("legacy top rebuilt", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().IsLoaded);
            AddUntilStep("modern filter hidden again", () => !SongSelect.ChildrenOfType<FilterControl>().Single().IsPresent);
        }
    }
}

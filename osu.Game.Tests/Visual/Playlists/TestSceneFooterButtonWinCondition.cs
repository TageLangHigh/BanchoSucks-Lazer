// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays;
using osu.Game.Screens.OnlinePlay;

namespace osu.Game.Tests.Visual.Playlists
{
    public partial class TestSceneFooterButtonWinCondition : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private readonly FooterButtonWinCondition button;

        public TestSceneFooterButtonWinCondition()
        {
            Add(button = new FooterButtonWinCondition
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.CentreLeft,
                X = -100,
            });

            // Reset logic is in popover.
            button.GetPopover();
        }

        [Test]
        public void TestWinConditionScore()
        {
            AddStep("win through score", () => button.CurrentCondition.Value = WinCondition.Score);
        }

        [Test]
        public void TestWinConditionAccuracy()
        {
            AddStep("win through score", () => button.CurrentCondition.Value = WinCondition.Accuracy);
        }

        [Test]
        public void TestResetToScoreWhenFreestyleIsEnabled()
        {
            AddStep("win through combo", () => button.CurrentCondition.Value = WinCondition.Combo);
            AddStep("enable freestyle", () => button.Freestyle.Value = true);
            AddAssert("win condition is score", () => button.CurrentCondition.Value == WinCondition.Score);
        }
    }
}

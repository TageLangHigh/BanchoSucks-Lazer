// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>
    /// Streamer mode inside the running game: the global action, the blocked chat key and the on-screen toast.
    /// </summary>
    public partial class TestSceneBanchosucksStreamerModeGame : OsuGameTestScene
    {
        private ChatOverlay chat => Game.ChildrenOfType<ChatOverlay>().Single();

        [Test]
        public void TestToggleActionAndBlockedChat()
        {
            AddUntilStep("wait for overlays", () => Game.ChildrenOfType<ChatOverlay>().Any(c => c.IsLoaded) && Game.ChildrenOfType<BanchosucksStreamerMode>().Any(c => c.IsLoaded));
            AddAssert("streamer mode is off by default", () => Game.LocalConfig.Get<bool>(OsuSetting.BanchosucksStreamerMode), () => Is.False);

            AddStep("open chat with F8", () => InputManager.Key(Key.F8));
            AddUntilStep("chat visible", () => chat.State.Value, () => Is.EqualTo(Visibility.Visible));

            AddStep("trigger streamer mode action", () => Game.ChildrenOfType<GlobalActionContainer>().First().TriggerPressed(GlobalAction.BanchosucksToggleStreamerMode));
            AddAssert("streamer mode on", () => Game.LocalConfig.Get<bool>(OsuSetting.BanchosucksStreamerMode));
            AddUntilStep("chat was closed", () => chat.State.Value, () => Is.EqualTo(Visibility.Hidden));
            AddUntilStep("toast shown", () => Game.ChildrenOfType<BanchosucksStreamerMode.BanchosucksStreamerModeToast>().Any());

            AddStep("press F8", () => InputManager.Key(Key.F8));
            AddAssert("chat stays hidden", () => chat.State.Value, () => Is.EqualTo(Visibility.Hidden));

            AddStep("trigger streamer mode action again", () => Game.ChildrenOfType<GlobalActionContainer>().First().TriggerPressed(GlobalAction.BanchosucksToggleStreamerMode));
            AddAssert("streamer mode off", () => Game.LocalConfig.Get<bool>(OsuSetting.BanchosucksStreamerMode), () => Is.False);

            AddStep("press F8", () => InputManager.Key(Key.F8));
            AddUntilStep("chat visible again", () => chat.State.Value, () => Is.EqualTo(Visibility.Visible));
        }

        [Test]
        public void TestActionHasNoDefaultKeyButIsListed()
        {
            AddAssert("listed without a key", () => new GlobalActionContainer(null).DefaultKeyBindings
                                                                                  .Single(b => b.GetAction<GlobalAction>() == GlobalAction.BanchosucksToggleStreamerMode).KeyCombination.Keys.Single(),
                () => Is.EqualTo(InputKey.None));
            AddAssert("last global action", () => (int)GlobalAction.BanchosucksToggleStreamerMode, () => Is.GreaterThan((int)GlobalAction.BanchosucksBossKey));
        }
    }
}

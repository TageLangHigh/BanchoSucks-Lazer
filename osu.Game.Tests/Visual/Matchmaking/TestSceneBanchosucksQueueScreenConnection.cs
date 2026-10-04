// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Matchmaking;
using osu.Game.Screens.OnlinePlay.Matchmaking.Intro;
using osu.Game.Screens.OnlinePlay.Matchmaking.Queue;
using osu.Game.Tests.Visual.Multiplayer;

namespace osu.Game.Tests.Visual.Matchmaking
{
    /// <summary>
    /// Banchosucks: the "Begin queueing" button must never write into the multiplayer client's connection state.
    /// </summary>
    /// <remarks>
    /// The button used to bind its <c>Enabled</c> to the client's <c>IsConnected</c>. Bindings work in both directions,
    /// so on a server without matchmaking pools (no pool selected, button disabled) the client considered itself
    /// disconnected after one visit to this screen: "Create room" stayed greyed out and joining rooms failed.
    /// </remarks>
    public partial class TestSceneBanchosucksQueueScreenConnection : MultiplayerTestScene
    {
        private ScreenQueue? queueScreen => Stack.CurrentScreen as ScreenQueue;

        private ShearedButton? beginQueueingButton
            => queueScreen?.ChildrenOfType<ShearedButton>().FirstOrDefault(b => b.Text.ToString() == "Begin queueing");

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("load screen", () => LoadScreen(new ScreenIntro(MatchmakingPoolType.QuickPlay)));
            AddUntilStep("wait for queue screen", () => queueScreen?.IsLoaded == true);
            AddStep("show idle state", () => queueScreen!.SetState(ScreenQueue.MatchmakingScreenState.Idle));
            AddUntilStep("button loaded", () => beginQueueingButton?.IsLoaded == true);
        }

        [Test]
        public void TestNoPoolDoesNotDisconnectClient()
        {
            AddStep("no pool selected", () => QueueController.SelectedPool.Value = null);

            AddAssert("client still connected", () => MultiplayerClient.IsConnected.Value);
            AddUntilStep("button disabled", () => beginQueueingButton?.Enabled.Value == false);
            AddAssert("client still connected afterwards", () => MultiplayerClient.IsConnected.Value);
        }

        [Test]
        public void TestButtonFollowsPoolAndConnection()
        {
            AddUntilStep("pool selected", () => QueueController.SelectedPool.Value != null);
            AddUntilStep("button enabled", () => beginQueueingButton?.Enabled.Value == true);

            AddStep("disconnect client", () => MultiplayerClient.Disconnect());
            AddUntilStep("button disabled", () => beginQueueingButton?.Enabled.Value == false);

            AddStep("reconnect client", () => MultiplayerClient.Connect());
            AddUntilStep("client connected", () => MultiplayerClient.IsConnected.Value);
        }
    }
}

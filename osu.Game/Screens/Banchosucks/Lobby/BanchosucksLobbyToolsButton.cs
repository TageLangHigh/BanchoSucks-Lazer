// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Online.Multiplayer;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.OnlinePlay.Match.Components;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: opens the lobby tournament tools. Only the host of the room sees it, like the game's own "Add item"
    /// button follows who may add maps: the room state is read when the room changes, nothing is bound.
    /// </summary>
    public partial class BanchosucksLobbyToolsButton : PurpleRoundedButton
    {
        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        public BanchosucksLobbyToolsButton()
        {
            Text = ClickerStrings.Text("Tournament tools", "Turnier-Werkzeug");
            Alpha = 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            client.RoomUpdated += onRoomUpdated;
            onRoomUpdated();
        }

        private void onRoomUpdated() => Alpha = client.Room != null && client.IsHost ? 1 : 0;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
                client.RoomUpdated -= onRoomUpdated;
        }
    }
}

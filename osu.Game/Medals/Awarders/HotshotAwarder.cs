// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.Ranking;

namespace osu.Game.Medals.Awarders
{
    /// <summary>
    /// "Hotshot" medal awarder (ID: 353)
    /// Awarded for being the only player to achieve a full combo in multiplayer with at least four players.
    /// <see href="https://inex.osekai.net/medals/Hotshot">Solution Reference (Osekai INEX)</see>
    /// </summary>
    public class HotshotAwarder : IMedalAwarder
    {
        private const int min_players = 4;

        public int MedalId => 353;
        public bool Enabled { get; set; }

        private OsuScreenStack? screenStack;
        private IAPIProvider? api;

        public bool CheckMedalCriteria(OsuGameBase game)
        {
            // When could the game has more than one OsuScreenStack? This is weird but CI did complain about that.
            screenStack ??= game.ChildrenOfType<OsuScreenStack>().FirstOrDefault();
            api ??= (IAPIProvider)game.Dependencies.Get(typeof(IAPIProvider));

            if (screenStack?.CurrentScreen is not MultiplayerResultsScreen resultsScreen || api == null)
                return false;

            if (resultsScreen.Score == null || resultsScreen.Score.OnlineID <= 0)
                return false;

            int localUserId = api.LocalUser.Value.OnlineID;

            if (localUserId <= 1)
                return false;

            bool localPlayerFullCombo = false;
            bool otherPlayerFullCombo = false;
            int playerCount = 0;

            foreach (ScoreInfo score in resultsScreen.ChildrenOfType<ScorePanel>().Select(panel => panel.Score))
            {
                bool isFullCombo = score.MaxCombo == score.GetMaximumAchievableCombo();

                if (score.UserID == localUserId)
                    localPlayerFullCombo = isFullCombo;
                else
                    otherPlayerFullCombo |= isFullCombo;

                playerCount++;
            }

            return localPlayerFullCombo && !otherPlayerFullCombo && playerCount >= min_players;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Overlays.Profile.Sections.Ranks
{
    /// <summary>
    /// Banchosucks: the context menu of a score in the profile's score lists. Currently one entry, "render as video",
    /// for osu!standard scores whose replay is on the server (see <see cref="BanchosucksReplayRenderer"/>).
    /// </summary>
    public partial class BanchosucksProfileScoreActions : Component
    {
        private readonly SoloScoreInfo score;

        [Resolved]
        private BanchosucksReplayRenderer? renderer { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private RealmAccess? realm { get; set; }

        [Resolved]
        private UserProfileOverlay? profileOverlay { get; set; }

        public BanchosucksProfileScoreActions(SoloScoreInfo score)
        {
            this.score = score;
        }

        /// <summary>
        /// Whether the score could be rendered: osu!standard, on the server, with a replay.
        /// </summary>
        public static bool CanRender(SoloScoreInfo score, RulesetStore rulesets)
        {
            if (score.OnlineID <= 0 || !score.HasReplay)
                return false;

            var ruleset = rulesets.GetRuleset(score.RulesetID);
            return ruleset != null && BanchosucksReplayRenderer.CanRenderRuleset(ruleset);
        }

        public MenuItem[] MenuItems
        {
            get
            {
                var items = new List<MenuItem>();

                if (renderer != null && LoadState >= LoadState.Ready && CanRender(score, rulesets))
                    items.Add(new OsuMenuItem(BanchosucksReplayRenderer.RenderActionText, MenuItemType.Standard, render));

                return items.ToArray();
            }
        }

        private void render()
        {
            if (renderer == null)
                return;

            renderer.Render(renderer.ResolveLocal(CreateScoreInfo(score, rulesets, localHash, profileOverlay?.Header.User.Value?.User)));
        }

        /// <summary>
        /// The hash of the local copy of a beatmap, used when the server did not send the beatmap's checksum.
        /// </summary>
        private string? localHash(int beatmapId) =>
            realm?.Run(r => r.All<BeatmapInfo>().FirstOrDefault(b => b.OnlineID == beatmapId)?.MD5Hash);

        /// <summary>
        /// Builds what the renderer needs from a profile score: the online id, the beatmap's hash (to find the local
        /// copy of the map) and the player's name for the file name.
        /// </summary>
        public static ScoreInfo CreateScoreInfo(SoloScoreInfo score, RulesetStore rulesets, Func<int, string?> localHash, APIUser? profileUser)
        {
            var info = score.ToScoreInfo(rulesets);

            string? checksum = score.Beatmap?.Checksum;
            if (string.IsNullOrEmpty(checksum))
                checksum = localHash(score.BeatmapID);

            info.BeatmapInfo = new BeatmapInfo
            {
                OnlineID = score.BeatmapID,
                MD5Hash = checksum ?? string.Empty,
            };

            // the score lists of a profile do not always repeat the player
            if (string.IsNullOrEmpty(info.User.Username) && profileUser != null)
                info.User = profileUser;

            return info;
        }
    }
}

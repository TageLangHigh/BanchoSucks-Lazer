// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Globalization;
using System.Net.Http;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Online.API;

namespace osu.Game.Online.Rooms
{
    public class CreateRoomScoreRequest : APIRequest<APIScoreToken>
    {
        private readonly long roomId;
        private readonly long playlistItemId;
        private readonly BeatmapInfo beatmapInfo;
        private readonly int rulesetId;
        private readonly string versionHash;
        private readonly string? rulesetHash;

        public CreateRoomScoreRequest(long roomId, long playlistItemId, BeatmapInfo beatmapInfo, int rulesetId, string versionHash, string? rulesetHash = null)
        {
            this.roomId = roomId;
            this.playlistItemId = playlistItemId;
            this.beatmapInfo = beatmapInfo;
            this.rulesetId = rulesetId;
            this.versionHash = versionHash;
            this.rulesetHash = rulesetHash;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.AddParameter("version_hash", versionHash);
            req.AddParameter("beatmap_id", beatmapInfo.OnlineID.ToString(CultureInfo.InvariantCulture));
            req.AddParameter("beatmap_hash", beatmapInfo.MD5Hash);
            req.AddParameter("ruleset_id", rulesetId.ToString(CultureInfo.InvariantCulture));

            if (!string.IsNullOrEmpty(rulesetHash))
                req.AddParameter("ruleset_hash", rulesetHash);
            return req;
        }

        protected override string Target => $@"rooms/{roomId}/playlist/{playlistItemId}/scores";
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

namespace osu.Game.Online
{
    public class ProductionEndpointConfiguration : EndpointConfiguration
    {
        public ProductionEndpointConfiguration()
        {
            // the lazer pages live under a path prefix on the main site; osu-web style
            // links (/users/{id}, /beatmapsets/{id}, /b/{id}, /scores/{id}) are routed there
            WebsiteUrl = @"https://banchosucks.cc/lazer";
            APIUrl = @"https://lazer-api.banchosucks.cc";
            APIClientSecret = @"FGc9GAtyHzeQDshWP5Ah7dega8hJACAJpQtw6OXk";
            APIClientID = "5";
            SpectatorUrl = @"https://lazer-api.banchosucks.cc/signalr/spectator";
            MultiplayerUrl = @"https://lazer-api.banchosucks.cc/signalr/multiplayer";
            MetadataUrl = @"https://lazer-api.banchosucks.cc/signalr/metadata";
            BeatmapSubmissionServiceUrl = @"https://lazer-api.banchosucks.cc/beatmap-submission";
        }
    }
}

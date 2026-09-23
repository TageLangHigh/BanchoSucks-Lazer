// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

namespace osu.Game.Online
{
    public class ProductionEndpointConfiguration : EndpointConfiguration
    {
        public ProductionEndpointConfiguration()
        {
            WebsiteUrl = @"https://lazer.banchosucks.cc";
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

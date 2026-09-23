// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Game.Configuration;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore
    {
        private readonly OsuConfigManager? configManager;

        public TrustedDomainOnlineStore(OsuConfigManager? configManager = null)
        {
            this.configManager = configManager;
        }

        protected override string GetLookupUrl(string url)
        {
            string? customApiUrl = configManager?.Get<string>(OsuSetting.CustomApiUrl);
            if (!string.IsNullOrWhiteSpace(customApiUrl) || (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                                                             (uri.Host.Equals("lazer-api.banchosucks.cc", StringComparison.OrdinalIgnoreCase) ||
                                                              uri.Host.Equals("lazer.banchosucks.cc", StringComparison.OrdinalIgnoreCase) ||
                                                              uri.Host.EndsWith(".ppy.sh", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".g0v0.top", StringComparison.OrdinalIgnoreCase))))
                return url;

            Logger.Log(
                $"[TrustedDomainOnlineStore] Blocked external resource lookup: {url}",
                LoggingTarget.Network,
                LogLevel.Important
            );

            return string.Empty;
        }
    }
}

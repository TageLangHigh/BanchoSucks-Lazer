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
            if (IsAllowedUrl(url, configManager))
                return url;

            Logger.Log(
                $"[TrustedDomainOnlineStore] Blocked external resource lookup: {url}",
                LoggingTarget.Network,
                LogLevel.Important
            );

            return string.Empty;
        }

        /// <summary>
        /// Whether <paramref name="url"/> may be retrieved as an online asset.
        /// </summary>
        /// <param name="url">The absolute URL to check.</param>
        /// <param name="configManager">Used to determine whether a custom API server is configured, which lifts the restriction.</param>
        public static bool IsAllowedUrl(string? url, OsuConfigManager? configManager = null)
        {
            // TODO: Don't think this is appropriate. The check is skipped when a custom endpoint is set. Needs a rewrite.
            string? customApiUrl = configManager?.Get<string>(OsuSetting.CustomApiUrl);
            if (!string.IsNullOrWhiteSpace(customApiUrl))
                return true;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   (uri.Host.Equals("banchosucks.cc", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith(".banchosucks.cc", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith(".ppy.sh", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".g0v0.top", StringComparison.OrdinalIgnoreCase));
        }
    }
}

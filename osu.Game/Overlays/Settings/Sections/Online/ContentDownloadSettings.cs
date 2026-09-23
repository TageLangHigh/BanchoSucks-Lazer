// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    public partial class ContentDownloadSettings : SettingsSubsection
    {
        protected override LocalisableString Header => OnlineSettingsStrings.ContentDownloadsHeader;

        private FormTextBox customApiUrlTextBox = null!;

        private readonly Bindable<SettingsNote.Data?> customApiNote = new Bindable<SettingsNote.Data?>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IAPIProvider api)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = OnlineSettingsStrings.ExternalLinkWarning,
                    Current = config.GetBindable<bool>(OsuSetting.ExternalLinkWarning)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = OnlineSettingsStrings.PreferNoVideo,
                    Current = config.GetBindable<bool>(OsuSetting.PreferNoVideo)
                })
                {
                    Keywords = new[] { "no-video" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = OnlineSettingsStrings.AutomaticallyDownloadMissingBeatmaps,
                    Current = config.GetBindable<bool>(OsuSetting.AutomaticallyDownloadMissingBeatmaps),
                })
                {
                    Keywords = new[] { "spectator", "replay" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = OnlineSettingsStrings.ShowExplicitContent,
                    Current = config.GetBindable<bool>(OsuSetting.ShowOnlineExplicitContent),
                })
                {
                    Keywords = new[] { "nsfw", "18+", "offensive" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = OnlineSettingsStrings.HideCountryFlags,
                    Current = config.GetBindable<bool>(OsuSetting.HideCountryFlags)
                }),
                new SettingsItemV2(customApiUrlTextBox = new FormTextBox
                {
                    Caption = OnlineSettingsStrings.CustomApiUrl,
                    Current = config.GetBindable<string>(OsuSetting.CustomApiUrl),
                    PlaceholderText = api.Endpoints.APIUrl,
                })
                {
                    Keywords = new[] { "api", "server", "custom", "endpoint" },
                    Note = { BindTarget = customApiNote },
                },
            };

            customApiUrlTextBox.Current.BindValueChanged(onCustomApiUrlChanged, true);
        }

        private string lastApiUrl = string.Empty;
        private bool isInitialLoad = true;
        private ScheduledDelegate? pendingValidation;
        private const double debounce_delay = 500;

        private static readonly Regex host_port_pattern = new Regex(
            pattern:
            @"^(?:" +
            @"(?:(?:[A-Za-z0-9-]+)\.)+[A-Za-z0-9-]+" +
            @"|" +
            @"(?:(?:25[0-5]|2[0-4]\d|1?\d{1,2})\.){3}(?:25[0-5]|2[0-4]\d|1?\d{1,2})" +
            @")(?::(?<port>\d{1,5}))?$",
            options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private void onCustomApiUrlChanged(ValueChangedEvent<string> e)
        {
            if (isInitialLoad)
            {
                string initRaw = e.NewValue.Trim();
                lastApiUrl = normalizeToHttps(initRaw);
                isInitialLoad = false;
                return;
            }

            pendingValidation?.Cancel();
            pendingValidation = Scheduler.AddDelayed(() =>
            {
                string rawInput = (customApiUrlTextBox.Current.Value ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(rawInput))
                {
                    maybeShowRestartIfChanged(string.Empty);
                    customApiNote.Value = null;
                    return;
                }

                string hostPort = stripSchemeAndPath(rawInput);

                if (!isValidHostPort(hostPort))
                {
                    customApiNote.Value = new SettingsNote.Data(OnlineSettingsStrings.CustomApiUrlInvalid, SettingsNote.Type.Critical);
                    return;
                }

                // This is troublesome and hence not allowed.
                if (hostPort.Contains(@"ppy.sh"))
                {
                    customApiNote.Value = new SettingsNote.Data(OnlineSettingsStrings.CustomApiRiskyWarning, SettingsNote.Type.Critical);
                    return;
                }

                string normalised = "https://" + hostPort;
                customApiNote.Value = null;
                maybeShowRestartIfChanged(normalised);
            }, debounce_delay);
        }

        private static bool isValidHostPort(string hostPort)
        {
            var m = host_port_pattern.Match(hostPort);
            if (!m.Success) return false;

            var g = m.Groups["port"];

            if (g.Success)
            {
                if (!int.TryParse(g.Value, out int port)) return false;
                if (port < 1 || port > 65535) return false;
            }

            return true;
        }

        private static string normalizeToHttps(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            string hostPort = stripSchemeAndPath(raw);
            return isValidHostPort(hostPort) ? "https://" + hostPort : hostPort;
        }

        private static string stripSchemeAndPath(string input)
        {
            string s = Regex.Replace(input, @"^\s*https?://", "", RegexOptions.IgnoreCase);

            int slash = s.IndexOf('/');
            if (slash >= 0) s = s.Substring(0, slash);

            s = s.TrimEnd('/');

            return s;
        }

        private void maybeShowRestartIfChanged(string normalizedNewValue)
        {
            if (!string.Equals(lastApiUrl, normalizedNewValue, StringComparison.OrdinalIgnoreCase))
            {
                lastApiUrl = normalizedNewValue;
                customApiNote.Value = new SettingsNote.Data(OnlineSettingsStrings.CustomApiUrlRestartRequired, SettingsNote.Type.Informational);
            }
        }
    }
}

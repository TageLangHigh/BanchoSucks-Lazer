// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Message;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Users;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace osu.Desktop
{
    internal partial class DiscordRichPresence : Component
    {
        /// <summary>
        /// Discord application built into the client, used until the server names the Banchosucks one.
        /// </summary>
        private const string client_id = "1216669957799018608";

        /// <summary>
        /// Banchosucks: server endpoint (banchosucks_client plugin) with the Discord application id and Rich Presence images.
        /// </summary>
        private const string remote_config_path = @"/api/plugins/banchosucks_client/config";

        private const string default_website_url = @"https://banchosucks.cc";

        private DiscordRpcClient? client;
        private string activeClientId = string.Empty;
        private Bindable<string> savedClientId = null!;
        private BanchosucksClientConfig? remoteConfig;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private LoginOverlay? login { get; set; }

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        [Resolved]
        private LocalUserStatisticsProvider statisticsProvider { get; set; } = null!;

        private IBindable<DiscordRichPresenceMode> privacyMode = null!;
        private IBindable<UserStatus> userStatus = null!;
        private IBindable<UserActivity?> userActivity = null!;

        private readonly RichPresence presence = new RichPresence
        {
            Assets = new Assets { LargeImageKey = "osu_logo_lazer" },
            Timestamps = Timestamps.Now,
            Secrets = new Secrets
            {
                JoinSecret = null,
                SpectateSecret = null,
            },
        };

        private IBindable<APIUser>? user;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, SessionStatics session)
        {
            privacyMode = config.GetBindable<DiscordRichPresenceMode>(OsuSetting.DiscordRichPresence);
            userStatus = config.GetBindable<UserStatus>(OsuSetting.UserOnlineStatus);
            userActivity = session.GetBindable<UserActivity?>(Static.UserOnlineActivity);

            // Banchosucks: start with the Discord application the server named last time, the built-in one otherwise
            savedClientId = config.GetBindable<string>(OsuSetting.BanchosucksDiscordAppId);
            createClient(string.IsNullOrEmpty(savedClientId.Value) ? client_id : savedClientId.Value);
        }

        private void createClient(string id)
        {
            if (client != null)
            {
                client.OnReady -= onReady;
                client.OnJoin -= onJoin;
                client.Dispose();
            }

            activeClientId = id;

            client = new DiscordRpcClient(id)
            {
                // SkipIdenticalPresence allows us to fire SetPresence at any point and leave it to the underlying implementation
                // to check whether a difference has actually occurred before sending a command to Discord (with a minor caveat that's handled in onReady).
                SkipIdenticalPresence = true
            };

            client.OnReady += onReady;
            client.OnError += (_, e) => Logger.Log($"An error occurred with Discord RPC Client: {e.Message} ({e.Code})", LoggingTarget.Network);

            try
            {
                client.RegisterUriScheme();
                client.Subscribe(EventType.Join);
                client.OnJoin += onJoin;
            }
            catch (Exception ex)
            {
                // This is known to fail in at least the following sandboxed environments:
                // - macOS (when packaged as an app bundle)
                // - flatpak (see: https://github.com/flathub/sh.ppy.osu/issues/170)
                // There is currently no better way to do this offered by Discord, so the best we can do is simply ignore it for now.
                Logger.Log($"Failed to register Discord URI scheme: {ex}");
            }

            client.Initialize();
        }

        /// <summary>
        /// Banchosucks: asks the server for the Discord application id and the Rich Presence images, so both can change without a new client build.
        /// </summary>
        private void fetchRemoteConfig()
        {
            var request = new OsuJsonWebRequest<BanchosucksClientConfig>($@"{api.Endpoints.APIUrl}{remote_config_path}");

            Task.Run(async () =>
            {
                try
                {
                    await request.PerformAsync().ConfigureAwait(false);
                    var response = request.ResponseObject;
                    Schedule(() => applyRemoteConfig(response));
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not load the Banchosucks client config: {e.Message}", LoggingTarget.Network);
                }
                finally
                {
                    request.Dispose();
                }
            });
        }

        private void applyRemoteConfig(BanchosucksClientConfig? config)
        {
            if (config == null)
                return;

            remoteConfig = config;

            string serverId = config.DiscordAppId?.Trim() ?? string.Empty;
            bool validId = serverId.Length > 0 && serverId.All(char.IsAsciiDigit);

            savedClientId.Value = validId ? serverId : string.Empty;

            string wanted = validId ? serverId : client_id;
            if (wanted != activeClientId)
                createClient(wanted);

            schedulePresenceUpdate();
        }

        private string websiteUrl
        {
            get
            {
                string? url = remoteConfig?.WebsiteUrl;
                return !string.IsNullOrEmpty(url) && url.StartsWith(@"https://", StringComparison.Ordinal) ? url.TrimEnd('/') : default_website_url;
            }
        }

        /// <summary>
        /// Banchosucks: absolute url of a Rich Presence image from the server config (Discord loads images by url), or null.
        /// </summary>
        private string? remoteImage(string name)
        {
            if (remoteConfig?.DiscordImages == null || !remoteConfig.DiscordImages.TryGetValue(name, out string? path) || string.IsNullOrEmpty(path))
                return null;

            string url = path.StartsWith('/') ? api.Endpoints.APIUrl + path : path;
            return url.Length <= 256 ? url : null;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            user = api.LocalUser.GetBoundCopy();

            ruleset.BindValueChanged(_ => schedulePresenceUpdate());
            userStatus.BindValueChanged(_ => schedulePresenceUpdate());
            userActivity.BindValueChanged(_ => schedulePresenceUpdate());
            privacyMode.BindValueChanged(_ => schedulePresenceUpdate());

            multiplayerClient.RoomUpdated += onRoomUpdated;
            statisticsProvider.StatisticsUpdated += onStatisticsUpdated;

            fetchRemoteConfig();
        }

        private void onReady(object sender, ReadyMessage __)
        {
            Logger.Log("Discord RPC Client ready.", LoggingTarget.Network, LogLevel.Debug);

            // when RPC is lost and reconnected, we have to clear presence state for updatePresence to work (see DiscordRpcClient.SkipIdenticalPresence).
            if (sender is DiscordRpcClient readyClient && readyClient.CurrentPresence != null)
                readyClient.SetPresence(null);

            schedulePresenceUpdate();
        }

        private void onRoomUpdated() => schedulePresenceUpdate();

        private void onStatisticsUpdated(UserStatisticsUpdate _) => schedulePresenceUpdate();

        private ScheduledDelegate? presenceUpdateDelegate;

        private void schedulePresenceUpdate()
        {
            presenceUpdateDelegate?.Cancel();
            presenceUpdateDelegate = Scheduler.AddDelayed(() =>
            {
                if (client == null || !client.IsInitialized)
                    return;

                if (!api.IsLoggedIn || userStatus.Value == UserStatus.Offline || privacyMode.Value == DiscordRichPresenceMode.Off)
                {
                    client.ClearPresence();
                    return;
                }

                bool hideIdentifiableInformation = privacyMode.Value == DiscordRichPresenceMode.Limited || userStatus.Value == UserStatus.DoNotDisturb;

                updatePresence(hideIdentifiableInformation);
                client.SetPresence(presence);
            }, 200);
        }

        private void updatePresence(bool hideIdentifiableInformation)
        {
            if (user == null)
                return;

            // user activity
            if (userActivity.Value != null)
            {
                presence.State = clampLength(userActivity.Value.GetStatus(hideIdentifiableInformation));
                presence.Details = clampLength(userActivity.Value.GetDetails(hideIdentifiableInformation) ?? string.Empty);

                var buttons = new List<Button>();

                if (userActivity.Value.GetBeatmapID(hideIdentifiableInformation) is int beatmapId && beatmapId > 0)
                {
                    buttons.Add(new Button
                    {
                        Label = "Beatmap ansehen",
                        Url = $@"{api.Endpoints.WebsiteUrl}/beatmaps/{beatmapId}?mode={ruleset.Value.ShortName}"
                    });
                }

                buttons.Add(websiteButton());
                presence.Buttons = buttons.ToArray();
            }
            else
            {
                presence.State = "Idle";
                presence.Details = string.Empty;
                presence.Buttons = new[] { websiteButton() };
            }

            // user party
            if (!hideIdentifiableInformation && multiplayerClient.Room != null && !multiplayerClient.Room.Settings.MatchType.IsMatchmakingType())
            {
                MultiplayerRoom room = multiplayerClient.Room;

                presence.Party = new Party
                {
                    Privacy = string.IsNullOrEmpty(room.Settings.Password) ? Party.PrivacySetting.Public : Party.PrivacySetting.Private,
                    ID = room.RoomID.ToString(),
                    // technically lobbies can have infinite users, but Discord needs this to be set to something.
                    // to make party display sensible, assign a powers of two above participants count (8 at minimum).
                    Max = (int)Math.Max(8, Math.Pow(2, Math.Ceiling(Math.Log2(room.Users.Count)))),
                    Size = room.Users.Count,
                };

                RoomSecret roomSecret = new RoomSecret
                {
                    RoomID = room.RoomID,
                    Password = room.Settings.Password,
                };

                if (client?.HasRegisteredUriScheme == true)
                    presence.Secrets.JoinSecret = JsonConvert.SerializeObject(roomSecret);

                // discord cannot handle both secrets and buttons at the same time, so we need to choose something.
                // the multiplayer room seems more important.
                presence.Buttons = null;
            }
            else
            {
                presence.Party = null;
                presence.Secrets.JoinSecret = null;
            }

            // game images (Banchosucks: logo and mode icons come from the server by url, the built-in asset keys are the fallback)
            presence.Assets.LargeImageKey = remoteImage(@"logo") ?? @"osu_logo_lazer";

            // large image tooltip
            if (privacyMode.Value == DiscordRichPresenceMode.Limited)
                presence.Assets.LargeImageText = @"BanchoSucks Lazer";
            else
            {
                var statistics = statisticsProvider.GetStatisticsFor(ruleset.Value);
                presence.Assets.LargeImageText = clampLength($"BanchoSucks Lazer · {user.Value.Username}" + (statistics?.GlobalRank > 0 ? $" (Rang #{statistics.GlobalRank:N0})" : string.Empty));
            }

            // small image
            string modeKey = ruleset.Value.IsLegacyRuleset() ? $"mode_{ruleset.Value.OnlineID}" : "mode_custom";
            presence.Assets.SmallImageKey = remoteImage(modeKey) ?? modeKey;
            presence.Assets.SmallImageText = ruleset.Value.Name;
        }

        private Button websiteButton() => new Button
        {
            Label = "Auf Banchosucks spielen",
            Url = websiteUrl,
        };

        private void onJoin(object sender, JoinMessage args) => Scheduler.AddOnce(() =>
        {
            game.Window?.Raise();

            if (!api.IsLoggedIn)
            {
                login?.Show();
                return;
            }

            Logger.Log($"Received room secret from Discord RPC Client: \"{args.Secret}\"", LoggingTarget.Network, LogLevel.Debug);

            // Stable and lazer share the same Discord client ID, meaning they can accept join requests from each other.
            // Since they aren't compatible in multi, see if stable's format is being used and log to avoid confusion.
            if (args.Secret[0] != '{' || !tryParseRoomSecret(args.Secret, out long roomId, out string? password))
            {
                Logger.Log("Could not join multiplayer room, invitation is invalid or incompatible.", LoggingTarget.Network, LogLevel.Important);
                return;
            }

            var request = new GetRoomRequest(roomId);
            request.Success += room => Schedule(() =>
            {
                game.PresentMultiplayerMatch(room, password);
            });
            request.Failure += _ => Logger.Log($"Could not join multiplayer room, room could not be found (room ID: {roomId}).", LoggingTarget.Network, LogLevel.Important);
            api.Queue(request);
        });

        private static readonly int ellipsis_length = Encoding.UTF8.GetByteCount(new[] { '…' });

        private static string clampLength(string str)
        {
            // Empty strings are fine to discord even though single-character strings are not. Make it make sense.
            if (string.IsNullOrEmpty(str))
                return str;

            // As above, discord decides that *non-empty* strings shorter than 2 characters cannot possibly be valid input, because... reasons?
            // And yes, that is two *characters*, or *codepoints*, not *bytes* as further down below (as determined by empirical testing).
            // Also, spaces don't count. Because reasons, clearly.
            // That all seems very questionable, and isn't even documented anywhere. So to *make it* accept such valid input,
            // just tack on enough of U+200B ZERO WIDTH SPACEs at the end. After making sure to trim whitespace.
            string trimmed = str.Trim();
            if (trimmed.Length < 2)
                return trimmed.PadRight(2, '\u200B');

            if (Encoding.UTF8.GetByteCount(str) <= 128)
                return str;

            ReadOnlyMemory<char> strMem = str.AsMemory();

            do
            {
                strMem = strMem[..^1];
            } while (Encoding.UTF8.GetByteCount(strMem.Span) + ellipsis_length > 128);

            return string.Create(strMem.Length + 1, strMem, (span, mem) =>
            {
                mem.Span.CopyTo(span);
                span[^1] = '…';
            });
        }

        private static bool tryParseRoomSecret(string secretJson, out long roomId, out string? password)
        {
            roomId = 0;
            password = null;

            RoomSecret? roomSecret;

            try
            {
                roomSecret = JsonConvert.DeserializeObject<RoomSecret>(secretJson);
            }
            catch
            {
                return false;
            }

            if (roomSecret == null) return false;

            roomId = roomSecret.RoomID;
            password = roomSecret.Password;

            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (multiplayerClient.IsNotNull())
                multiplayerClient.RoomUpdated -= onRoomUpdated;

            if (statisticsProvider.IsNotNull())
                statisticsProvider.StatisticsUpdated -= onStatisticsUpdated;

            client?.Dispose();
            base.Dispose(isDisposing);
        }

        private class BanchosucksClientConfig
        {
            [JsonProperty(@"discord_app_id")]
            public string? DiscordAppId { get; set; }

            [JsonProperty(@"discord_images")]
            public Dictionary<string, string>? DiscordImages { get; set; }

            [JsonProperty(@"website_url")]
            public string? WebsiteUrl { get; set; }
        }

        private class RoomSecret
        {
            [JsonProperty(@"roomId", Required = Required.Always)]
            public long RoomID { get; set; }

            [JsonProperty(@"password", Required = Required.AllowNull)]
            public string? Password { get; set; }
        }
    }
}

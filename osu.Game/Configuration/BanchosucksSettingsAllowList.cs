// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Configuration;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: what a settings export ("Einstellungen sichern") may contain. Everything is listed by hand;
    /// a setting that is not listed here is neither written to an export nor read from one. New settings are
    /// therefore left out until somebody decides they are safe and adds them.
    /// </summary>
    /// <remarks>
    /// An export is a file the player hands to other people ("here is my setup"). It must never carry the account
    /// (name, token, whether the password is saved), the server it talks to, anything that identifies the PC
    /// (window position, display, audio device, renderer) or bookkeeping of this installation.
    /// </remarks>
    public static class BanchosucksSettingsAllowList
    {
        /// <summary>
        /// Settings that must never be exported or imported, whatever the allow-list says. The export filters
        /// against this list a second time, and the tests check that none of them can appear in a file.
        /// </summary>
        public static readonly IReadOnlyList<OsuSetting> NeverExported = new[]
        {
            // the account
            OsuSetting.Username,
            OsuSetting.Token,
            OsuSetting.SavePassword,
            OsuSetting.SaveUsername,
            OsuSetting.WasSupporter,
            OsuSetting.UserOnlineStatus,

            // the server the client talks to, and what the server told it
            OsuSetting.CustomApiUrl,
            OsuSetting.BanchosucksDiscordAppId,

            // bookkeeping of this installation
            OsuSetting.Version,
            OsuSetting.ReleaseStream,
            OsuSetting.ShowFirstRunSetup,
            OsuSetting.ShowMobileDisclaimer,
            OsuSetting.LastProcessedMetadataId,
            OsuSetting.LastOnlineTagsPopulation,
            OsuSetting.DisableAutomaticUpdates,
            OsuSetting.BanchosucksPresetsHintShown,
            OsuSetting.BanchosucksStarRatingCheck,
            OsuSetting.BanchosucksStarRatingCheckPosition,

            // names of things that only exist on this PC (the skin travels by name in its own field)
            OsuSetting.Skin,
            OsuSetting.SongSelectCollectionFilter,
        };

        /// <summary>
        /// The game settings (osu.cfg) an export contains.
        /// </summary>
        public static readonly IReadOnlyList<OsuSetting> GameSettings = new[]
        {
            // gameplay
            OsuSetting.DimLevel,
            OsuSetting.BlurLevel,
            OsuSetting.LightenDuringBreaks,
            OsuSetting.ShowStoryboard,
            OsuSetting.KeyOverlay,
            OsuSetting.GameplayLeaderboard,
            OsuSetting.PositionalHitsoundsLevel,
            OsuSetting.AlwaysPlayFirstComboBreak,
            OsuSetting.FloatingComments,
            OsuSetting.HUDVisibilityMode,
            OsuSetting.ShowHealthDisplayWhenCantFail,
            OsuSetting.FadePlayfieldWhenHealthLow,
            OsuSetting.HitLighting,
            OsuSetting.StarFountains,
            OsuSetting.ScoreDisplayMode,
            OsuSetting.IncreaseFirstObjectVisibility,
            OsuSetting.GameplayDisableWinKey,
            OsuSetting.ReplaySettingsOverlay,
            OsuSetting.ReplayPlaybackControlsExpanded,
            OsuSetting.ComboColourNormalisationAmount,
            OsuSetting.AlwaysShowHoldForMenuButton,
            OsuSetting.AlwaysRequireHoldingForPause,
            OsuSetting.BeatmapSkins,
            OsuSetting.BeatmapColours,
            OsuSetting.BeatmapHitsounds,

            // audio
            OsuSetting.AudioOffset,
            OsuSetting.AutomaticallyAdjustBeatmapOffset,
            OsuSetting.VolumeInactive,
            OsuSetting.HitsoundVolume,
            OsuSetting.MenuMusic,
            OsuSetting.MenuVoice,

            // input
            OsuSetting.MenuCursorSize,
            OsuSetting.GameplayCursorSize,
            OsuSetting.AutoCursorSize,
            OsuSetting.GameplayCursorDuringTouch,
            OsuSetting.CursorRotation,
            OsuSetting.MouseDisableButtons,
            OsuSetting.MouseDisableWheel,
            OsuSetting.ConfineMouseMode,
            OsuSetting.TouchDisableGameplayTaps,

            // interface
            OsuSetting.MenuTips,
            OsuSetting.MenuParallaxScale,
            OsuSetting.Prefer24HourTime,
            OsuSetting.ShowFpsDisplay,
            OsuSetting.UIScale,
            OsuSetting.UIHoldActivationDelay,
            OsuSetting.MenuBackgroundSource,
            OsuSetting.SeasonalBackgroundMode,
            OsuSetting.PinnedSeasonalBackground,
            OsuSetting.PinnedSeasonalBackgroundIndex,
            OsuSetting.Scaling,
            OsuSetting.ScalingPositionX,
            OsuSetting.ScalingPositionY,
            OsuSetting.ScalingSizeX,
            OsuSetting.ScalingSizeY,
            OsuSetting.ScalingBackgroundDim,
            OsuSetting.SafeAreaConsiderations,
            OsuSetting.ScreenshotFormat,
            OsuSetting.ScreenshotCaptureMenuCursor,
            OsuSetting.ToolbarClockDisplayMode,
            OsuSetting.ToolbarButtons,
            OsuSetting.ToolbarAutoHideButtons,
            OsuSetting.ChatDisplayHeight,
            OsuSetting.ProfileCoverExpanded,
            OsuSetting.HideCountryFlags,
            OsuSetting.DashboardSortMode,
            OsuSetting.DashboardDisplayStyle,
            OsuSetting.BanchosucksMenuOpacity,
            OsuSetting.ToriiLegacyFooterUseSkin,
            OsuSetting.ToriiLegacySongSelectFooter,
            OsuSetting.ToriiStableResults,
            OsuSetting.ToriiLegacyFont,

            // song select and beatmap listing
            OsuSetting.BeatmapDetailTab,
            OsuSetting.BeatmapLeaderboardSortMode,
            OsuSetting.BeatmapDetailModsFilter,
            OsuSetting.DisplayStarsMinimum,
            OsuSetting.DisplayStarsMaximum,
            OsuSetting.SongSelectGroupMode,
            OsuSetting.SongSelectSortingMode,
            OsuSetting.SongSelectBackgroundBlur,
            OsuSetting.RandomSelectAlgorithm,
            OsuSetting.ModSelectHotkeyStyle,
            OsuSetting.ModSelectTextSearchStartsActive,
            OsuSetting.ShowConvertedBeatmaps,
            OsuSetting.BeatmapListingCardSize,
            OsuSetting.BeatmapListingFeaturedArtistFilter,

            // online behaviour (preferences, nothing about the account itself)
            OsuSetting.ExternalLinkWarning,
            OsuSetting.PreferNoVideo,
            OsuSetting.ShowOnlineExplicitContent,
            OsuSetting.AutomaticallyDownloadMissingBeatmaps,
            OsuSetting.NotifyOnUsernameMentioned,
            OsuSetting.NotifyOnPrivateMessage,
            OsuSetting.NotifyOnFriendPresenceChange,
            OsuSetting.PMFriendsOnly,
            OsuSetting.DiscordRichPresence,
            OsuSetting.MultiplayerRoomFilter,
            OsuSetting.MultiplayerShowInProgressFilter,
            OsuSetting.MultiplayerShowFullFilter,

            // editor
            OsuSetting.EditorDim,
            OsuSetting.EditorWaveformOpacity,
            OsuSetting.EditorShowHitMarkers,
            OsuSetting.EditorAutoSeekOnPlacement,
            OsuSetting.EditorLimitedDistanceSnap,
            OsuSetting.EditorShowSpeedChanges,
            OsuSetting.EditorScaleOrigin,
            OsuSetting.EditorRotationOrigin,
            OsuSetting.EditorAdjustExistingObjectsOnTimingChanges,
            OsuSetting.EditorTimelineShowTimingChanges,
            OsuSetting.EditorTimelineShowBreaks,
            OsuSetting.EditorTimelineShowTicks,
            OsuSetting.EditorContractSidebars,
            OsuSetting.EditorShowStoryboard,
            OsuSetting.EditorSubmissionNotifyOnDiscussionReplies,
            OsuSetting.EditorSubmissionLoadInBrowserAfterSubmission,

            // Banchosucks
            OsuSetting.BanchosucksInputRate,
            OsuSetting.BanchosucksAudioRate,
        };

        /// <summary>
        /// The framework settings (framework.ini) an export contains: only what means the same on another PC.
        /// Window size and position, display, resolution, renderer and audio device are deliberately missing.
        /// </summary>
        public static readonly IReadOnlyList<FrameworkSetting> FrameworkSettings = new[]
        {
            FrameworkSetting.FrameSync,
            FrameworkSetting.ExecutionMode,
            FrameworkSetting.VolumeUniversal,
            FrameworkSetting.VolumeEffect,
            FrameworkSetting.VolumeMusic,
            FrameworkSetting.ShowUnicode,
        };

        public const string MOUSE_SENSITIVITY = "Sensitivity";
        public const string MOUSE_RELATIVE_MODE = "UseRelativeMode";

        /// <summary>
        /// The mouse handler values an export contains (they live in input.json, not in a config manager).
        /// </summary>
        public static readonly IReadOnlyList<string> MouseSettings = new[] { MOUSE_SENSITIVITY, MOUSE_RELATIVE_MODE };

        /// <summary>
        /// The per-ruleset settings an export contains, by ruleset short name. osu.Game cannot see the rulesets'
        /// own enums, so the keys are listed by name (checked against the enums in the tests).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RulesetSettings = new Dictionary<string, IReadOnlyList<string>>
        {
            ["osu"] = new[]
            {
                "SnakingInSliders", "SnakingOutSliders", "HitAnimations", "ShowCursorTrail", "ShowCursorRipples", "PlayfieldBorderStyle",
                "ReplayClickMarkersEnabled", "ReplayFrameMarkersEnabled", "ReplayCursorPathEnabled", "ReplayCursorHideEnabled", "ReplayAnalysisDisplayLength",
            },
            ["taiko"] = new[] { "TouchControlScheme", "RateAdjustedHitAnimation", "HitAnimations" },
            ["mania"] = new[] { "ScrollSpeed", "ScrollDirection", "TimingBasedNoteColouring", "MobileLayout", "TouchOverlay" },
        };

        private static readonly HashSet<string> game_names = new HashSet<string>(GameSettings.Except(NeverExported).Select(s => s.ToString()), StringComparer.Ordinal);
        private static readonly HashSet<string> framework_names = new HashSet<string>(FrameworkSettings.Select(s => s.ToString()), StringComparer.Ordinal);

        /// <summary>
        /// The game settings that are really exported: the allow-list minus <see cref="NeverExported"/>.
        /// </summary>
        public static IEnumerable<OsuSetting> ExportedGameSettings => GameSettings.Except(NeverExported);

        public static bool IsGameSettingAllowed(string name) => game_names.Contains(name);

        public static bool IsFrameworkSettingAllowed(string name) => framework_names.Contains(name);

        public static bool IsMouseSettingAllowed(string name) => MouseSettings.Contains(name);

        public static bool IsRulesetSettingAllowed(string ruleset, string key)
            => RulesetSettings.TryGetValue(ruleset, out var keys) && keys.Contains(key);
    }
}

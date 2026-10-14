// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Audio;
using osu.Framework.Configuration;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Platform;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: one-click presets (Settings > General > Presets). "Smooth" is the setup that runs without
    /// stutter on most PCs, "Stable feel" makes lazer look and score like osu!stable. Both only write settings;
    /// the live skin and the Classic mod are switched by the caller (they need the running game).
    /// </summary>
    public static class BanchosucksPresets
    {
        public static void ApplySmooth(FrameworkConfigManager frameworkConfig, OsuConfigManager config, AudioManager? audio = null, MouseHandler? mouse = null)
        {
            // "unlimited" is the most common cause of stutter; upstream recommends twice the refresh rate
            frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Limit2x);
            frameworkConfig.SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.MultiThreaded);

            if (audio != null && RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
                audio.UseExperimentalWasapi.Value = true;

            if (mouse != null)
                mouse.UseRelativeMode.Value = true;

            // visual extras that cost frames during a play
            config.SetValue(OsuSetting.ShowStoryboard, false);
            config.SetValue(OsuSetting.HitLighting, false);
            config.SetValue(OsuSetting.BlurLevel, 0.0);

            // higher thread rates cost CPU time that a stuttering PC does not have
            config.SetValue(OsuSetting.BanchosucksInputRate, BanchosucksThreadRate.Default);
            config.SetValue(OsuSetting.BanchosucksAudioRate, BanchosucksThreadRate.Default);
        }

        public static void ApplyStableFeel(OsuConfigManager config)
        {
            config.SetValue(OsuSetting.Skin, SkinInfo.CLASSIC_SKIN.ToString());
            config.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Classic);
            config.SetValue(OsuSetting.BeatmapSkins, true);
            config.SetValue(OsuSetting.BeatmapColours, true);
            config.SetValue(OsuSetting.BeatmapHitsounds, true);
            config.SetValue(OsuSetting.KeyOverlay, true);
            // stable has no hold-to-confirm; upstream's own "classic default" for this setting
            config.SetValue(OsuSetting.UIHoldActivationDelay, 0.0);
        }
    }
}

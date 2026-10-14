// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksPresetsTest
    {
        [Test]
        public void TestSmoothPresetRewritesPerformanceSettings()
        {
            using (var storage = new TemporaryNativeStorage(nameof(BanchosucksPresetsTest)))
            using (var frameworkConfig = new FrameworkConfigManager(storage))
            using (var config = new OsuConfigManager(storage))
            {
                frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Unlimited);
                frameworkConfig.SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.SingleThread);
                config.SetValue(OsuSetting.HitLighting, true);
                config.SetValue(OsuSetting.ShowStoryboard, true);
                config.SetValue(OsuSetting.BlurLevel, 0.5);
                config.SetValue(OsuSetting.BanchosucksInputRate, BanchosucksThreadRate.Hz8000);

                BanchosucksPresets.ApplySmooth(frameworkConfig, config);

                Assert.That(frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync), Is.EqualTo(FrameSync.Limit2x));
                Assert.That(frameworkConfig.Get<ExecutionMode>(FrameworkSetting.ExecutionMode), Is.EqualTo(ExecutionMode.MultiThreaded));
                Assert.That(config.Get<bool>(OsuSetting.HitLighting), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.ShowStoryboard), Is.False);
                Assert.That(config.Get<double>(OsuSetting.BlurLevel), Is.EqualTo(0));
                Assert.That(config.Get<BanchosucksThreadRate>(OsuSetting.BanchosucksInputRate), Is.EqualTo(BanchosucksThreadRate.Default));
            }
        }

        [Test]
        public void TestStableFeelPresetSelectsClassicLookAndScoring()
        {
            using (var storage = new TemporaryNativeStorage(nameof(BanchosucksPresetsTest)))
            using (var config = new OsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.KeyOverlay, false);
                config.SetValue(OsuSetting.BeatmapSkins, false);

                BanchosucksPresets.ApplyStableFeel(config);

                Assert.That(Guid.Parse(config.Get<string>(OsuSetting.Skin)), Is.EqualTo(new Guid("81F02CD3-EEC6-4865-AC23-FAE26A386187")), "classic skin");
                Assert.That(config.Get<ScoringMode>(OsuSetting.ScoreDisplayMode), Is.EqualTo(ScoringMode.Classic));
                Assert.That(config.Get<bool>(OsuSetting.KeyOverlay), Is.True);
                Assert.That(config.Get<bool>(OsuSetting.BeatmapSkins), Is.True);
                Assert.That(config.Get<double>(OsuSetting.UIHoldActivationDelay), Is.EqualTo(0));
            }
        }
    }
}

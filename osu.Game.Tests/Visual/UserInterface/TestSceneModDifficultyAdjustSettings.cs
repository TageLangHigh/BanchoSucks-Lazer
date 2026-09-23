// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneModDifficultyAdjustSettings : OsuManualInputManagerTestScene
    {
        private OsuModDifficultyAdjust modDifficultyAdjust;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create control", () =>
            {
                modDifficultyAdjust = new OsuModDifficultyAdjust();

                Child = new Container
                {
                    Size = new Vector2(300),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = Color4.Black,
                            RelativeSizeAxes = Axes.Both,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ChildrenEnumerable = modDifficultyAdjust.CreateSettingsControls(),
                        },
                    }
                };
            });
        }

        [Test]
        public void TestFollowsBeatmapDefaultsVisually()
        {
            setBeatmapWithDifficultyParameters(5);

            checkSliderAtValue(SongSelectStrings.CircleSize, 5);
            checkBindableAtValue(SongSelectStrings.CircleSize, null);

            setBeatmapWithDifficultyParameters(8);

            checkSliderAtValue(SongSelectStrings.CircleSize, 8);
            checkBindableAtValue(SongSelectStrings.CircleSize, null);
        }

        [Test]
        public void TestValueAboveRangeStillApplied()
        {
            AddStep("set override cs to 11", () => modDifficultyAdjust.CircleSize.Value = 11);

            checkSliderAtValue(SongSelectStrings.CircleSize, 11);
            checkBindableAtValue(SongSelectStrings.CircleSize, 11);

            // this is a no-op, just showing that it won't reset the value during deserialisation.
            setExtendedLimits(false);

            checkSliderAtValue(SongSelectStrings.CircleSize, 11);
            checkBindableAtValue(SongSelectStrings.CircleSize, 11);

            // setting extended limits will reset the serialisation exception.
            // this should be fine as the goal is to allow, at most, the value of extended limits.
            setExtendedLimits(true);

            checkSliderAtValue(SongSelectStrings.CircleSize, 11);
            checkBindableAtValue(SongSelectStrings.CircleSize, 11);
        }

        [Test]
        public void TestValueBelowRangeStillApplied()
        {
            AddStep("set override cs to -5", () => modDifficultyAdjust.ApproachRate.Value = -5);

            checkSliderAtValue(SongSelectStrings.ApproachRate, -5);
            checkBindableAtValue(SongSelectStrings.ApproachRate, -5);

            // this is a no-op, just showing that it won't reset the value during deserialisation.
            setExtendedLimits(false);

            checkSliderAtValue(SongSelectStrings.ApproachRate, -5);
            checkBindableAtValue(SongSelectStrings.ApproachRate, -5);

            // setting extended limits will reset the serialisation exception.
            // this should be fine as the goal is to allow, at most, the value of extended limits.
            setExtendedLimits(true);

            checkSliderAtValue(SongSelectStrings.ApproachRate, -5);
            checkBindableAtValue(SongSelectStrings.ApproachRate, -5);
        }

        [Test]
        public void TestExtendedLimits()
        {
            setSliderValue(SongSelectStrings.CircleSize, 99);

            checkSliderAtValue(SongSelectStrings.CircleSize, 10);
            checkBindableAtValue(SongSelectStrings.CircleSize, 10);

            setExtendedLimits(true);

            checkSliderAtValue(SongSelectStrings.CircleSize, 10);
            checkBindableAtValue(SongSelectStrings.CircleSize, 10);

            setSliderValue(SongSelectStrings.CircleSize, 99);

            checkSliderAtValue(SongSelectStrings.CircleSize, 11);
            checkBindableAtValue(SongSelectStrings.CircleSize, 11);

            setSliderValue(SongSelectStrings.ApproachRate, -5);

            checkSliderAtValue(SongSelectStrings.ApproachRate, -5);
            checkBindableAtValue(SongSelectStrings.ApproachRate, -5);

            setExtendedLimits(false);

            checkSliderAtValue(SongSelectStrings.CircleSize, 10);
            checkBindableAtValue(SongSelectStrings.CircleSize, 10);
        }

        [Test]
        public void TestUserOverrideMaintainedOnBeatmapChange()
        {
            setSliderValue(SongSelectStrings.CircleSize, 9);

            setBeatmapWithDifficultyParameters(2);

            checkSliderAtValue(SongSelectStrings.CircleSize, 9);
            checkBindableAtValue(SongSelectStrings.CircleSize, 9);
        }

        [Test]
        public void TestExtendedLimitsRetainedAfterBoundCopyCreation()
        {
            setExtendedLimits(true);
            setSliderValue(SongSelectStrings.CircleSize, 11);

            checkSliderAtValue(SongSelectStrings.CircleSize, 11);
            checkBindableAtValue(SongSelectStrings.CircleSize, 11);

            AddStep("create bound copy", () => _ = modDifficultyAdjust.CircleSize.GetBoundCopy());

            checkSliderAtValue(SongSelectStrings.CircleSize, 11);
            checkBindableAtValue(SongSelectStrings.CircleSize, 11);
        }

        [Test]
        public void TestResetToDefault()
        {
            setBeatmapWithDifficultyParameters(2);

            setSliderValue(SongSelectStrings.CircleSize, 9);
            checkSliderAtValue(SongSelectStrings.CircleSize, 9);
            checkBindableAtValue(SongSelectStrings.CircleSize, 9);

            resetToDefault(SongSelectStrings.CircleSize);
            checkSliderAtValue(SongSelectStrings.CircleSize, 2);
            checkBindableAtValue(SongSelectStrings.CircleSize, null);
        }

        [Test]
        public void TestUserOverrideMaintainedOnMatchingBeatmapValue()
        {
            setBeatmapWithDifficultyParameters(3);

            checkSliderAtValue(SongSelectStrings.CircleSize, 3);
            checkBindableAtValue(SongSelectStrings.CircleSize, null);

            // need to initially change it away from the current beatmap value to trigger an override.
            setSliderValue(SongSelectStrings.CircleSize, 4);
            setSliderValue(SongSelectStrings.CircleSize, 3);

            checkSliderAtValue(SongSelectStrings.CircleSize, 3);
            checkBindableAtValue(SongSelectStrings.CircleSize, 3);

            setBeatmapWithDifficultyParameters(4);

            checkSliderAtValue(SongSelectStrings.CircleSize, 3);
            checkBindableAtValue(SongSelectStrings.CircleSize, 3);
        }

        [Test]
        public void TestResetToDefaults()
        {
            setBeatmapWithDifficultyParameters(5);

            setSliderValue(SongSelectStrings.CircleSize, 3);
            setExtendedLimits(true);

            checkSliderAtValue(SongSelectStrings.CircleSize, 3);
            checkBindableAtValue(SongSelectStrings.CircleSize, 3);

            AddStep("reset mod settings", () => modDifficultyAdjust.ResetSettingsToDefaults());

            checkSliderAtValue(SongSelectStrings.CircleSize, 5);
            checkBindableAtValue(SongSelectStrings.CircleSize, null);
        }

        [Test]
        public void TestResetToDefaultViaDoubleClickingNub()
        {
            setBeatmapWithDifficultyParameters(5);

            setSliderValue(SongSelectStrings.CircleSize, 3);
            setExtendedLimits(true);

            checkSliderAtValue(SongSelectStrings.CircleSize, 3);
            checkBindableAtValue(SongSelectStrings.CircleSize, 3);

            AddStep("double click circle size nub", () =>
            {
                var nub = this.ChildrenOfType<RoundedSliderBar<float>.SliderNub>().First();
                InputManager.MoveMouseTo(nub);
                InputManager.Click(MouseButton.Left);
                InputManager.Click(MouseButton.Left);
            });

            checkSliderAtValue(SongSelectStrings.CircleSize, 5);
            checkBindableAtValue(SongSelectStrings.CircleSize, null);
        }

        [Test]
        public void TestModSettingChangeTracker()
        {
            ModSettingChangeTracker tracker = null;
            Queue<Mod> settingsChangedQueue = null;

            setBeatmapWithDifficultyParameters(5);

            AddStep("add mod settings change tracker", () =>
            {
                settingsChangedQueue = new Queue<Mod>();

                tracker = new ModSettingChangeTracker(modDifficultyAdjust.Yield())
                {
                    SettingChanged = settingsChangedQueue.Enqueue
                };
            });

            AddAssert("no settings changed", () => settingsChangedQueue.Count == 0);

            setSliderValue(SongSelectStrings.CircleSize, 3);

            settingsChangedFired();

            setSliderValue(SongSelectStrings.CircleSize, 5);
            checkBindableAtValue(SongSelectStrings.CircleSize, 5);

            settingsChangedFired();

            AddStep("reset mod settings", () => modDifficultyAdjust.CircleSize.SetDefault());
            checkBindableAtValue(SongSelectStrings.CircleSize, null);

            settingsChangedFired();

            setExtendedLimits(true);

            settingsChangedFired();

            AddStep("dispose tracker", () =>
            {
                tracker.Dispose();
                tracker = null;
            });

            void settingsChangedFired()
            {
                AddAssert("setting changed event fired", () =>
                {
                    settingsChangedQueue.Dequeue();
                    return settingsChangedQueue.Count == 0;
                });
            }
        }

        private void resetToDefault(LocalisableString name)
        {
            AddStep($"Reset {name} to default", () =>
                this.ChildrenOfType<DifficultyAdjustSettingsControl>().First(c => c.LabelText == name)
                    .Current.SetDefault());
        }

        private void setExtendedLimits(bool status) =>
            AddStep($"Set extended limits {status}", () => modDifficultyAdjust.ExtendedLimits.Value = status);

        private void setSliderValue(LocalisableString name, float value)
        {
            AddStep($"Set {name} slider to {value}", () =>
                this.ChildrenOfType<DifficultyAdjustSettingsControl>().First(c => c.LabelText == name)
                    .ChildrenOfType<RoundedSliderBar<float>>().First().Current.Value = value);
        }

        private void checkBindableAtValue(LocalisableString name, float? expectedValue)
        {
            AddAssert($"Bindable {name} is {(expectedValue?.ToString() ?? "null")}", () =>
                this.ChildrenOfType<DifficultyAdjustSettingsControl>().First(c => c.LabelText == name)
                    .Current.Value == expectedValue);
        }

        private void checkSliderAtValue(LocalisableString name, float expectedValue)
        {
            AddAssert($"Slider {name} at {expectedValue}", () =>
                this.ChildrenOfType<DifficultyAdjustSettingsControl>().First(c => c.LabelText == name)
                    .ChildrenOfType<RoundedSliderBar<float>>().First().Current.Value == expectedValue);
        }

        private void setBeatmapWithDifficultyParameters(float value)
        {
            AddStep($"set beatmap with all {value}", () => Beatmap.Value = CreateWorkingBeatmap(new Beatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Difficulty = new BeatmapDifficulty
                    {
                        OverallDifficulty = value,
                        CircleSize = value,
                        DrainRate = value,
                        ApproachRate = value,
                    }
                }
            }));
        }
    }
}

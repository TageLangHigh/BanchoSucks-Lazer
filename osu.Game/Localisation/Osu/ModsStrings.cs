// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation.Osu
{
    public static class ModsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.Osu.Mods";

        /// <summary>
        /// "Don't use the same key twice in a row!"
        /// </summary>
        public static LocalisableString AlternateDescription => new TranslatableString(getKey(@"alternate_description"), @"Don't use the same key twice in a row!");

        /// <summary>
        /// "Never trust the approach circles..."
        /// </summary>
        public static LocalisableString ApproachDifferentDescription => new TranslatableString(getKey(@"approach_different_description"), @"Never trust the approach circles...");

        /// <summary>
        /// "Automatic cursor movement - just follow the rhythm."
        /// </summary>
        public static LocalisableString AutopilotDescription => new TranslatableString(getKey(@"autopilot_description"), @"Automatic cursor movement - just follow the rhythm.");

        /// <summary>
        /// "Play with blinds on your screen."
        /// </summary>
        public static LocalisableString BlindsDescription => new TranslatableString(getKey(@"blinds_description"), @"Play with blinds on your screen.");

        /// <summary>
        /// "The cursor blooms into.. a larger cursor!"
        /// </summary>
        public static LocalisableString BloomDescription => new TranslatableString(getKey(@"bloom_description"), @"The cursor blooms into.. a larger cursor!");

        /// <summary>
        /// "Don't let their popping distract you!"
        /// </summary>
        public static LocalisableString BubblesDescription => new TranslatableString(getKey(@"bubbles_description"), @"Don't let their popping distract you!");

        /// <summary>
        /// "3D. Almost."
        /// </summary>
        public static LocalisableString DepthDescription => new TranslatableString(getKey(@"depth_description"), @"3D. Almost.");

        /// <summary>
        /// "Larger circles, more forgiving HP drain, less accuracy required, and extra lives!"
        /// </summary>
        public static LocalisableString EasyDescription => new TranslatableString(getKey(@"easy_description"), @"Larger circles, more forgiving HP drain, less accuracy required, and extra lives!");

        /// <summary>
        /// "Burn the notes into your memory."
        /// </summary>
        public static LocalisableString FreezeFrameDescription => new TranslatableString(getKey(@"freeze_frame_description"), @"Burn the notes into your memory.");

        /// <summary>
        /// "Play with no approach circles and fading circles/sliders."
        /// </summary>
        public static LocalisableString HiddenDescription => new TranslatableString(getKey(@"hidden_description"), @"Play with no approach circles and fading circles/sliders.");

        /// <summary>
        /// "No need to chase the circles – your cursor is a magnet!"
        /// </summary>
        public static LocalisableString MagnetisedDescription => new TranslatableString(getKey(@"magnetised_description"), @"No need to chase the circles – your cursor is a magnet!");

        /// <summary>
        /// "Flip objects on the chosen axes."
        /// </summary>
        public static LocalisableString MirrorDescription => new TranslatableString(getKey(@"mirror_description"), @"Flip objects on the chosen axes.");

        /// <summary>
        /// "Where's the cursor?"
        /// </summary>
        public static LocalisableString NoScopeDescription => new TranslatableString(getKey(@"no_scope_description"), @"Where's the cursor?");

        /// <summary>
        /// "It never gets boring!"
        /// </summary>
        public static LocalisableString RandomDescription => new TranslatableString(getKey(@"random_description"), @"It never gets boring!");

        /// <summary>
        /// "You don't need to click. Give your clicking/tapping fingers a break from the heat of things."
        /// </summary>
        public static LocalisableString RelaxDescription => new TranslatableString(getKey(@"relax_description"), @"You don't need to click. Give your clicking/tapping fingers a break from the heat of things.");

        /// <summary>
        /// "You must only use one key!"
        /// </summary>
        public static LocalisableString SingleTapDescription => new TranslatableString(getKey(@"single_tap_description"), @"You must only use one key!");

        /// <summary>
        /// "Circles spin in. No approach circles."
        /// </summary>
        public static LocalisableString SpinInDescription => new TranslatableString(getKey(@"spin_in_description"), @"Circles spin in. No approach circles.");

        /// <summary>
        /// "Spinners will be automatically completed."
        /// </summary>
        public static LocalisableString SpunOutDescription => new TranslatableString(getKey(@"spun_out_description"), @"Spinners will be automatically completed.");

        /// <summary>
        /// "Once you start a slider, follow precisely or get a miss."
        /// </summary>
        public static LocalisableString StrictTrackingDescription => new TranslatableString(getKey(@"strict_tracking_description"), @"Once you start a slider, follow precisely or get a miss.");

        /// <summary>
        /// "Practice keeping up with the beat of the song."
        /// </summary>
        public static LocalisableString TargetPracticeDescription => new TranslatableString(getKey(@"target_practice_description"), @"Practice keeping up with the beat of the song.");

        /// <summary>
        /// "Put your faith in the approach circles..."
        /// </summary>
        public static LocalisableString TraceableDescription => new TranslatableString(getKey(@"traceable_description"), @"Put your faith in the approach circles...");

        /// <summary>
        /// "Everything rotates. EVERYTHING."
        /// </summary>
        public static LocalisableString TransformDescription => new TranslatableString(getKey(@"transform_description"), @"Everything rotates. EVERYTHING.");

        /// <summary>
        /// "They just won't stay still..."
        /// </summary>
        public static LocalisableString WiggleDescription => new TranslatableString(getKey(@"wiggle_description"), @"They just won't stay still...");

        /// <summary>
        /// "Hit them at the right size!"
        /// </summary>
        public static LocalisableString ObjectScaleTweenDescription => new TranslatableString(getKey(@"object_scale_tween_description"), @"Hit them at the right size!");

        /// <summary>
        /// "Hit objects run away!"
        /// </summary>
        public static LocalisableString RepelDescription => new TranslatableString(getKey(@"repel_description"), @"Hit objects run away!");

        /// <summary>
        /// "Initial size"
        /// </summary>
        public static LocalisableString ApproachDifferentInitialSizeLabel => new TranslatableString(getKey(@"approach_different_initial_size_label"), @"Initial size");

        /// <summary>
        /// "Change the initial size of the approach circle, relative to hit circles."
        /// </summary>
        public static LocalisableString ApproachDifferentInitialSizeDescription => new TranslatableString(getKey(@"approach_different_initial_size_description"), @"Change the initial size of the approach circle, relative to hit circles.");

        /// <summary>
        /// "Style"
        /// </summary>
        public static LocalisableString ApproachDifferentStyleLabel => new TranslatableString(getKey(@"approach_different_style_label"), @"Style");

        /// <summary>
        /// "Change the animation style of the approach circles."
        /// </summary>
        public static LocalisableString ApproachDifferentStyleDescription => new TranslatableString(getKey(@"approach_different_style_description"), @"Change the animation style of the approach circles.");

        /// <summary>
        /// "Max size at combo"
        /// </summary>
        public static LocalisableString BloomMaxSizeAtComboLabel => new TranslatableString(getKey(@"bloom_max_size_at_combo_label"), @"Max size at combo");

        /// <summary>
        /// "The combo count at which the cursor reaches its maximum size"
        /// </summary>
        public static LocalisableString BloomMaxSizeAtComboDescription => new TranslatableString(getKey(@"bloom_max_size_at_combo_description"), @"The combo count at which the cursor reaches its maximum size");

        /// <summary>
        /// "Final size multiplier"
        /// </summary>
        public static LocalisableString BloomFinalSizeMultiplierLabel => new TranslatableString(getKey(@"bloom_final_size_multiplier_label"), @"Final size multiplier");

        /// <summary>
        /// "The multiplier applied to cursor size when combo reaches maximum"
        /// </summary>
        public static LocalisableString BloomFinalSizeMultiplierDescription => new TranslatableString(getKey(@"bloom_final_size_multiplier_description"), @"The multiplier applied to cursor size when combo reaches maximum");

        /// <summary>
        /// "No slider head accuracy requirement"
        /// </summary>
        public static LocalisableString ClassicNoSliderHeadAccuracyLabel => new TranslatableString(getKey(@"classic_no_slider_head_accuracy_requirement_label"), @"No slider head accuracy requirement");

        /// <summary>
        /// "Scores sliders proportionally to the number of ticks hit."
        /// </summary>
        public static LocalisableString ClassicNoSliderHeadAccuracyDescription => new TranslatableString(getKey(@"classic_no_slider_head_accuracy_requirement_description"), @"Scores sliders proportionally to the number of ticks hit.");

        /// <summary>
        /// "Apply classic note lock"
        /// </summary>
        public static LocalisableString ClassicApplyNoteLockLabel => new TranslatableString(getKey(@"classic_apply_note_lock_label"), @"Apply classic note lock");

        /// <summary>
        /// "Applies note lock to the full hit window."
        /// </summary>
        public static LocalisableString ClassicApplyNoteLockDescription => new TranslatableString(getKey(@"classic_apply_note_lock_description"), @"Applies note lock to the full hit window.");

        /// <summary>
        /// "Always play a slider's tail sample"
        /// </summary>
        public static LocalisableString ClassicAlwaysPlayTailSampleLabel => new TranslatableString(getKey(@"classic_always_play_slider_tail_sample_label"), @"Always play a slider's tail sample");

        /// <summary>
        /// "Always plays a slider's tail sample regardless of whether it was hit or not."
        /// </summary>
        public static LocalisableString ClassicAlwaysPlayTailSampleDescription => new TranslatableString(getKey(@"classic_always_play_slider_tail_sample_description"), @"Always plays a slider's tail sample regardless of whether it was hit or not.");

        /// <summary>
        /// "Fade out hit circles earlier"
        /// </summary>
        public static LocalisableString ClassicFadeOutHitCirclesEarlierLabel => new TranslatableString(getKey(@"classic_fade_out_hit_circles_earlier_label"), @"Fade out hit circles earlier");

        /// <summary>
        /// "Make hit circles fade out into a miss, rather than after it."
        /// </summary>
        public static LocalisableString ClassicFadeOutHitCirclesEarlierDescription => new TranslatableString(getKey(@"classic_fade_out_hit_circles_earlier_description"), @"Make hit circles fade out into a miss, rather than after it.");

        /// <summary>
        /// "Classic health"
        /// </summary>
        public static LocalisableString ClassicHealthLabel => new TranslatableString(getKey(@"classic_health_label"), @"Classic health");

        /// <summary>
        /// "More closely resembles the original HP drain mechanics."
        /// </summary>
        public static LocalisableString ClassicHealthDescription => new TranslatableString(getKey(@"classic_health_description"), @"More closely resembles the original HP drain mechanics.");

        /// <summary>
        /// "Maximum depth"
        /// </summary>
        public static LocalisableString DepthMaximumDepthLabel => new TranslatableString(getKey(@"depth_maximum_depth_label"), @"Maximum depth");

        /// <summary>
        /// "How far away objects appear."
        /// </summary>
        public static LocalisableString DepthMaximumDepthDescription => new TranslatableString(getKey(@"depth_maximum_depth_description"), @"How far away objects appear.");

        /// <summary>
        /// "Show Approach Circles"
        /// </summary>
        public static LocalisableString DepthShowApproachCirclesLabel => new TranslatableString(getKey(@"depth_show_approach_circles_label"), @"Show Approach Circles");

        /// <summary>
        /// "Whether approach circles should be visible."
        /// </summary>
        public static LocalisableString DepthShowApproachCirclesDescription => new TranslatableString(getKey(@"depth_show_approach_circles_description"), @"Whether approach circles should be visible.");

        /// <summary>
        /// "Follow delay"
        /// </summary>
        public static LocalisableString FlashlightFollowDelayLabel => new TranslatableString(getKey(@"flashlight_follow_delay_label"), @"Follow delay");

        /// <summary>
        /// "Milliseconds until the flashlight reaches the cursor"
        /// </summary>
        public static LocalisableString FlashlightFollowDelayDescription => new TranslatableString(getKey(@"flashlight_follow_delay_description"), @"Milliseconds until the flashlight reaches the cursor");

        /// <summary>
        /// "Only fade approach circles"
        /// </summary>
        public static LocalisableString HiddenOnlyFadeApproachCirclesLabel => new TranslatableString(getKey(@"hidden_only_fade_approach_circles_label"), @"Only fade approach circles");

        /// <summary>
        /// "The main object body will not fade when enabled."
        /// </summary>
        public static LocalisableString HiddenOnlyFadeApproachCirclesDescription => new TranslatableString(getKey(@"hidden_only_fade_approach_circles_description"), @"The main object body will not fade when enabled.");

        /// <summary>
        /// "Attraction strength"
        /// </summary>
        public static LocalisableString MagnetisedAttractionStrengthLabel => new TranslatableString(getKey(@"magnetised_attraction_strength_label"), @"Attraction strength");

        /// <summary>
        /// "How strong the pull is."
        /// </summary>
        public static LocalisableString MagnetisedAttractionStrengthDescription => new TranslatableString(getKey(@"magnetised_attraction_strength_description"), @"How strong the pull is.");

        /// <summary>
        /// "Flipped axes"
        /// </summary>
        public static LocalisableString MirrorFlippedAxesLabel => new TranslatableString(getKey(@"mirror_flipped_axes_label"), @"Flipped axes");

        /// <summary>
        /// "Starting Size"
        /// </summary>
        public static LocalisableString ObjectScaleTweenStartingSizeLabel => new TranslatableString(getKey(@"object_scale_tween_starting_size_label"), @"Starting Size");

        /// <summary>
        /// "The initial size multiplier applied to all objects."
        /// </summary>
        public static LocalisableString ObjectScaleTweenStartingSizeDescription => new TranslatableString(getKey(@"object_scale_tween_starting_size_description"), @"The initial size multiplier applied to all objects.");

        /// <summary>
        /// "Angle sharpness"
        /// </summary>
        public static LocalisableString RandomAngleSharpnessLabel => new TranslatableString(getKey(@"random_angle_sharpness_label"), @"Angle sharpness");

        /// <summary>
        /// "How sharp angles should be"
        /// </summary>
        public static LocalisableString RandomAngleSharpnessDescription => new TranslatableString(getKey(@"random_angle_sharpness_description"), @"How sharp angles should be");

        /// <summary>
        /// "Repulsion strength"
        /// </summary>
        public static LocalisableString RepelRepulsionStrengthLabel => new TranslatableString(getKey(@"repel_repulsion_strength_label"), @"Repulsion strength");

        /// <summary>
        /// "How strong the repulsion is."
        /// </summary>
        public static LocalisableString RepelRepulsionStrengthDescription => new TranslatableString(getKey(@"repel_repulsion_strength_description"), @"How strong the repulsion is.");

        /// <summary>
        /// "Also fail when missing a slider tail"
        /// </summary>
        public static LocalisableString SuddenDeathAlsoFailSliderTailLabel => new TranslatableString(getKey(@"sudden_death_also_fail_slider_tail_label"), @"Also fail when missing a slider tail");

        /// <summary>
        /// "Metronome ticks"
        /// </summary>
        public static LocalisableString TargetPracticeMetronomeTicksLabel => new TranslatableString(getKey(@"target_practice_metronome_ticks_label"), @"Metronome ticks");

        /// <summary>
        /// "Whether a metronome beat should play in the background"
        /// </summary>
        public static LocalisableString TargetPracticeMetronomeTicksDescription => new TranslatableString(getKey(@"target_practice_metronome_ticks_description"), @"Whether a metronome beat should play in the background");

        /// <summary>
        /// "Strength"
        /// </summary>
        public static LocalisableString WiggleStrengthLabel => new TranslatableString(getKey(@"wiggle_strength_label"), @"Strength");

        /// <summary>
        /// "Multiplier applied to the wiggling strength."
        /// </summary>
        public static LocalisableString WiggleStrengthDescription => new TranslatableString(getKey(@"wiggle_strength_description"), @"Multiplier applied to the wiggling strength.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

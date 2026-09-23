// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class RelaxAimEvaluator
    {
        private const double wide_angle_multiplier = 1.5;
        private const double acute_angle_multiplier = 2.6;
        private const double slider_multiplier = 1.5;
        private const double velocity_change_multiplier = 1.2;
        private const double wiggle_multiplier = 1.02;

        public static double EvaluateDifficultyOf(OsuDifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1)
                return 0;

            if (current.Previous(0) is not OsuDifficultyHitObject last || current.Previous(1) is not OsuDifficultyHitObject lastLast || last.BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            // Calculate the velocity to the current hitobject, which starts
            // with a base distance / time assuming the last object is a hitcircle.
            double currVelocity = current.LazyJumpDistance / current.AdjustedDeltaTime;

            // But if the last object is a slider, then we extend the travel velocity through the slider into the current object.
            if (withSliderTravelDistance && last.BaseObject is Slider)
            {
                double travelVelocity = last.TravelDistance / last.TravelTime;
                double movementVelocity = current.MinimumJumpDistance / current.MinimumJumpTime;
                currVelocity = Math.Max(currVelocity, movementVelocity + travelVelocity);
            }

            // As above, do the same for the previous hitobject.
            double prevVelocity = last.LazyJumpDistance / last.AdjustedDeltaTime;

            if (withSliderTravelDistance && lastLast.BaseObject is Slider)
            {
                double travelVelocity = lastLast.TravelDistance / lastLast.TravelTime;
                double movementVelocity = last.MinimumJumpDistance / last.MinimumJumpTime;
                prevVelocity = Math.Max(prevVelocity, movementVelocity + travelVelocity);
            }

            double wideAngleBonus = 0;
            double acuteAngleBonus = 0;
            double sliderBonus = 0;
            double velocityChangeBonus = 0;
            double wiggleBonus = 0;

            // Start strain with regular velocity.
            double aimStrain = currVelocity;

            // R* Penalize overall stream aim. Fittings: [(100, 0.92), (300, 0.98)] linear function.
            double streamNerf = current.LazyJumpDistance * 0.0006 + 0.86;
            aimStrain *= Math.Clamp(streamNerf, 0.92, 0.98);

            // If rhythms are the same.
            if (Math.Max(current.AdjustedDeltaTime, last.AdjustedDeltaTime) < 1.25 * Math.Min(current.AdjustedDeltaTime, last.AdjustedDeltaTime) && current.Angle is double currAngle
                && last.Angle is double lastAngle)
            {
                double angleBonus = Math.Min(currVelocity, prevVelocity);

                wideAngleBonus = calcWideAngleBonus(currAngle);
                acuteAngleBonus = calcAcuteAngleBonus(currAngle);

                // Penalize angle repetition.
                wideAngleBonus *= 1 - Math.Min(wideAngleBonus, DiffUtils.Pow(calcWideAngleBonus(lastAngle), 3));
                acuteAngleBonus *= 0.08 + 0.92 * (1 - Math.Min(acuteAngleBonus, DiffUtils.Pow(calcAcuteAngleBonus(lastAngle), 3)));

                // R* Nerf strain time for above 300 1/2 fast objects smoothly.
                const double nerf_base = 1.07;
                double nerfAdjustedDeltaTime = current.AdjustedDeltaTime
                                               * DiffUtils.Pow(nerf_base, DiffUtils.Smootherstep(DiffUtils.MillisecondsToBPM(current.AdjustedDeltaTime, 2), 300, 400));

                // Apply full wide angle bonus for distance more than one diameter.
                wideAngleBonus *= angleBonus * DiffUtils.Smootherstep(current.LazyJumpDistance, 0, diameter);

                // Apply acute angle bonus for BPM above 300 1/2 and distance more than one diameter.
                acuteAngleBonus *= angleBonus
                                   * DiffUtils.Smootherstep(DiffUtils.MillisecondsToBPM(nerfAdjustedDeltaTime, 2), 300, 400)
                                   * DiffUtils.Smootherstep(current.LazyJumpDistance, diameter, diameter * 2);

                // R* Penalize wide angles if their distances are quite small (consider as wide angle stream).
                double wideStreamNerf = current.LazyJumpDistance * 0.007 - 1.3;
                wideAngleBonus *= Math.Clamp(wideStreamNerf, 0, 1);

                // Apply wiggle bonus for jumps that are [radius, 3*diameter] in distance, with < 110 angle.
                wiggleBonus = angleBonus
                              * DiffUtils.Smootherstep(current.LazyJumpDistance, radius, diameter)
                              * DiffUtils.Pow(DiffUtils.ReverseLerp(current.LazyJumpDistance, diameter * 3, diameter), 1.8)
                              * DiffUtils.Smootherstep(currAngle, double.DegreesToRadians(110), double.DegreesToRadians(60))
                              * DiffUtils.Smootherstep(last.LazyJumpDistance, radius, diameter)
                              * DiffUtils.Pow(DiffUtils.ReverseLerp(last.LazyJumpDistance, diameter * 3, diameter), 1.8)
                              * DiffUtils.Smootherstep(lastAngle, double.DegreesToRadians(110), double.DegreesToRadians(60));
            }

            if (Math.Max(prevVelocity, currVelocity) != 0)
            {
                prevVelocity = (last.LazyJumpDistance + lastLast.TravelDistance) / last.AdjustedDeltaTime;
                currVelocity = (current.LazyJumpDistance + last.TravelDistance) / current.AdjustedDeltaTime;

                double distRatioBase = Math.Sin(Math.PI / 2 * Math.Abs(prevVelocity - currVelocity) / Math.Max(prevVelocity, currVelocity));
                double distRatio = DiffUtils.Pow(distRatioBase, 2);

                double overlapVelocityBuff = Math.Min(diameter * 1.25 / Math.Min(current.AdjustedDeltaTime, last.AdjustedDeltaTime), Math.Abs(prevVelocity - currVelocity));

                velocityChangeBonus = overlapVelocityBuff * distRatio;

                double bonusBase = Math.Min(current.AdjustedDeltaTime, last.AdjustedDeltaTime) / Math.Max(current.AdjustedDeltaTime, last.AdjustedDeltaTime);
                velocityChangeBonus *= DiffUtils.Pow(bonusBase, 2);
            }

            if (last.BaseObject is Slider)
                sliderBonus = last.TravelDistance / last.TravelTime;

            aimStrain += wiggleBonus * wiggle_multiplier;

            // Add in acute angle bonus or wide angle bonus + velocity change bonus, whichever is larger.
            aimStrain += Math.Max(acuteAngleBonus * acute_angle_multiplier, wideAngleBonus * wide_angle_multiplier + velocityChangeBonus * velocity_change_multiplier);

            if (withSliderTravelDistance)
                aimStrain += sliderBonus * slider_multiplier;

            if (current.LazyJumpDistance < 350)
                aimStrain *= RelaxRhythmEvaluator.EvaluateDifficultyOf(current, current.HitWindowGreat);

            return aimStrain;
        }

        private static double calcWideAngleBonus(double angle) => DiffUtils.Smoothstep(angle, double.DegreesToRadians(40), double.DegreesToRadians(140));

        private static double calcAcuteAngleBonus(double angle) => DiffUtils.Smoothstep(angle, double.DegreesToRadians(140), double.DegreesToRadians(40));
    }
}

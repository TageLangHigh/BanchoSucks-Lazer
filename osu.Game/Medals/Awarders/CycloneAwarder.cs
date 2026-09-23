// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets.UI;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Medals.Awarders
{
    /// <summary>
    /// "Cyclone" medal awarder (ID: 354)
    /// Awarded when a spinner reaches 477 SPM (spins per minute) in osu! standard mode.
    /// <see href="https://inex.osekai.net/medals/Cyclone">Solution Reference (Osekai INEX)</see>
    /// </summary>
    public class CycloneAwarder : IMedalAwarder
    {
        private const string osu_ruleset_short_name = "osu";
        private const string spinner_type_name = "osu.Game.Rulesets.Osu.Objects.Drawables.DrawableSpinner, osu.Game.Rulesets.Osu";
        private const double spm_threshold = 477.0;

        private static readonly Type? drawable_spinner_type = Type.GetType(spinner_type_name);
        private static readonly PropertyInfo? spinner_spins_per_minute_property = drawable_spinner_type?.GetProperty("SpinsPerMinute");
        private static readonly PropertyInfo? bindable_value_property = spinner_spins_per_minute_property?.PropertyType.GetProperty("Value");

        public int MedalId => 354;
        public bool Enabled { get; set; }

        private OsuScreenStack? screenStack;
        private SubmittingPlayer? currentPlayer;
        private HitObjectContainer? hitObjectContainer;

        public bool CheckMedalCriteria(OsuGameBase game)
        {
            if (drawable_spinner_type == null || spinner_spins_per_minute_property == null || bindable_value_property == null)
                return false;

            screenStack ??= game.ChildrenOfType<OsuScreenStack>().SingleOrDefault();

            if (screenStack?.CurrentScreen is not SubmittingPlayer player)
                return false;

            if (currentPlayer != player)
            {
                currentPlayer = player;
                hitObjectContainer = null;
            }

            if (!string.Equals(player.Score.ScoreInfo.Ruleset.ShortName, osu_ruleset_short_name, StringComparison.Ordinal))
                return false;

            hitObjectContainer ??= player.ChildrenOfType<HitObjectContainer>().SingleOrDefault();

            if (hitObjectContainer == null)
                return false;

            foreach (Drawable drawable in hitObjectContainer.AliveObjects)
            {
                if (!drawable_spinner_type.IsInstanceOfType(drawable))
                    continue;

                object? spinsPerMinuteBindable = spinner_spins_per_minute_property.GetValue(drawable);

                if (spinsPerMinuteBindable == null)
                    continue;

                if (bindable_value_property.GetValue(spinsPerMinuteBindable) is double spinsPerMinute && spinsPerMinute >= spm_threshold)
                    return true;
            }

            return false;
        }
    }
}

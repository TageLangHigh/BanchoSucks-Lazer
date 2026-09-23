// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Cursor;

namespace osu.Game.Medals.Awarders
{
    /// <summary>
    /// "Hamster Wheel" medal awarder (ID: 355)
    /// Awarded when a spinner reaches 277 SPM (spins per minute) in osu! standard mode.
    /// <see href="https://inex.osekai.net/medals/Hamster%20Wheel">Solution Reference (Osekai INEX)</see>
    /// </summary>
    public class HamsterWheelAwarder : IMedalAwarder
    {
        private const float rotation_threshold = 36000f;
        private const float return_tolerance = 0.5f;

        private static readonly FieldInfo? active_cursor_field = typeof(MenuCursorContainer).GetField("activeCursor", BindingFlags.Instance | BindingFlags.NonPublic);

        public int MedalId => 355;
        public bool Enabled { get; set; }

        private MenuCursorContainer? cursorContainer;
        private float maxRotation;

        public bool CheckMedalCriteria(OsuGameBase game)
        {
            if (active_cursor_field == null)
                return false;

            cursorContainer ??= game.ChildrenOfType<MenuCursorContainer>().SingleOrDefault();

            if (cursorContainer == null || string.Equals(cursorContainer.State.Value.ToString(), "Hidden", StringComparison.Ordinal))
                return false;

            if (active_cursor_field.GetValue(cursorContainer) is not Drawable activeCursor)
                return false;

            maxRotation = Math.Max(maxRotation, Math.Abs(activeCursor.Rotation));

            return maxRotation >= rotation_threshold && Math.Abs(activeCursor.Rotation) <= return_tolerance;
        }
    }
}

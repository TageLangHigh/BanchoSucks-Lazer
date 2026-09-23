// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Medals.Awarders
{
    /// <summary>
    /// "Courier Catapult" medal awarder (ID: 356)
    /// Awarded when a notification slides at a speed exceeding 3.5 px/ms.
    /// <see href="https://inex.osekai.net/medals/Courier%20Catapult">Solution Reference (Osekai INEX)</see>
    /// </summary>
    public class CourierCatapultAwarder : IMedalAwarder
    {
        public int MedalId => 356;
        public bool Enabled { get; set; }

        private NotificationOverlayToastTray? toastTray;
        private Dictionary<Notification, float> previousPositions = new Dictionary<Notification, float>();
        private Dictionary<Notification, float> currentPositions = new Dictionary<Notification, float>();

        private const double speed_threshold = 3.5;

        public bool CheckMedalCriteria(OsuGameBase game)
        {
            toastTray ??= game.ChildrenOfType<NotificationOverlayToastTray>().SingleOrDefault();

            if (toastTray == null)
                return false;

            if (!toastTray.IsDisplayingToasts)
            {
                previousPositions.Clear();
                currentPositions.Clear();
                return false;
            }

            double elapsedFrameTime = game.Clock.ElapsedFrameTime;

            foreach (Notification notification in toastTray.Notifications)
            {
                float? x = notification.MainContent.Parent?.X;
                if (x.HasValue)
                    currentPositions[notification] = x.Value;
            }

            if (elapsedFrameTime > 0)
            {
                foreach (var (notification, currentX) in currentPositions)
                {
                    if (!previousPositions.TryGetValue(notification, out float previousX))
                        continue;

                    double speed = Math.Abs((currentX - previousX) / elapsedFrameTime);

                    if (speed > speed_threshold)
                    {
                        return true;
                    }
                }
            }

            var temp = previousPositions;
            previousPositions = currentPositions;
            currentPositions = temp;
            currentPositions.Clear();

            return false;
        }
    }
}

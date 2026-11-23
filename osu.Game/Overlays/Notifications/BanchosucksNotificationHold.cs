// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Overlays.Notifications
{
    /// <summary>
    /// Banchosucks: why a notification is being held back instead of shown.
    /// </summary>
    [Flags]
    public enum BanchosucksHoldReason
    {
        None = 0,

        /// <summary>
        /// "Do not disturb": the player is in a map (playing, in a break or paused).
        /// </summary>
        Gameplay = 1,

        /// <summary>
        /// Streamer mode is on.
        /// </summary>
        StreamerMode = 2,
    }

    /// <summary>
    /// Banchosucks: what a notification is about, as far as holding it back is concerned.
    /// </summary>
    public enum BanchosucksNotificationKind
    {
        /// <summary>
        /// Marked critical by its sender (outage, failed score submission): never held.
        /// </summary>
        Critical,

        /// <summary>
        /// An error message: never held, it keeps the behaviour of the notification overlay.
        /// </summary>
        Error,

        /// <summary>
        /// A running task with a progress bar (import, download, export): never held, its completion would otherwise get lost.
        /// </summary>
        Progress,

        /// <summary>
        /// A private chat message.
        /// </summary>
        PrivateMessage,

        /// <summary>
        /// The player's name was mentioned in a chat channel.
        /// </summary>
        ChatMention,

        /// <summary>
        /// A friend came online or went offline.
        /// </summary>
        Friend,

        /// <summary>
        /// Another notification about a specific other player (multiplayer invitation).
        /// </summary>
        OtherPlayer,

        /// <summary>
        /// Everything else (finished downloads, hints, ...).
        /// </summary>
        Other,
    }

    /// <summary>
    /// Banchosucks: the rules of "do not disturb" and streamer mode, without any drawable in sight.
    /// </summary>
    public static class BanchosucksNotificationPolicy
    {
        /// <summary>
        /// Which of the <paramref name="active"/> reasons hold a notification of the given <paramref name="kind"/> back.
        /// </summary>
        public static BanchosucksHoldReason ReasonsFor(BanchosucksNotificationKind kind, BanchosucksHoldReason active)
        {
            var reasons = BanchosucksHoldReason.None;

            switch (kind)
            {
                case BanchosucksNotificationKind.Critical:
                case BanchosucksNotificationKind.Error:
                case BanchosucksNotificationKind.Progress:
                    return reasons;

                case BanchosucksNotificationKind.PrivateMessage:
                case BanchosucksNotificationKind.ChatMention:
                case BanchosucksNotificationKind.Friend:
                    // these show chat contents or who the streamer's friends are
                    reasons |= active & BanchosucksHoldReason.StreamerMode;
                    break;
            }

            reasons |= active & BanchosucksHoldReason.Gameplay;
            return reasons;
        }
    }

    /// <summary>
    /// Banchosucks: what <see cref="BanchosucksNotificationHold{T}.SetActive"/> lets go when a reason ends.
    /// </summary>
    public readonly struct BanchosucksHoldRelease<T>
    {
        /// <summary>
        /// The reason that just ended; <see cref="BanchosucksHoldReason.None"/> when nothing changed.
        /// </summary>
        public readonly BanchosucksHoldReason EndedReason;

        /// <summary>
        /// The items that are no longer held by anything, oldest first.
        /// </summary>
        public readonly IReadOnlyList<T> Items;

        public BanchosucksHoldRelease(BanchosucksHoldReason endedReason, IReadOnlyList<T> items)
        {
            EndedReason = endedReason;
            Items = items;
        }
    }

    /// <summary>
    /// Banchosucks: bookkeeping shared by "do not disturb" and streamer mode: items are held while a reason that applies
    /// to their kind is active, and handed back in one batch when the last such reason ends.
    /// </summary>
    /// <remarks>
    /// Not thread-safe; the caller locks. Generic so the rules can be tested without notifications.
    /// </remarks>
    public sealed class BanchosucksNotificationHold<T>
    {
        /// <summary>
        /// More than this many items are never kept; the oldest are dropped (and counted in <see cref="Dropped"/>).
        /// </summary>
        public const int CAPACITY = 200;

        public BanchosucksHoldReason Active { get; private set; }

        public int Count => held.Count;

        /// <summary>
        /// How many held items were dropped since the last release because <see cref="CAPACITY"/> was reached.
        /// </summary>
        public int Dropped { get; private set; }

        private readonly List<(T item, BanchosucksNotificationKind kind)> held = new List<(T, BanchosucksNotificationKind)>();

        /// <summary>
        /// Takes the item if any active reason applies to its kind.
        /// </summary>
        /// <returns>Whether the item is held now; if not, the caller shows it as usual.</returns>
        public bool TryHold(T item, BanchosucksNotificationKind kind)
        {
            if (BanchosucksNotificationPolicy.ReasonsFor(kind, Active) == BanchosucksHoldReason.None)
                return false;

            if (held.Count >= CAPACITY)
            {
                held.RemoveAt(0);
                Dropped++;
            }

            held.Add((item, kind));
            return true;
        }

        /// <summary>
        /// Switches one reason on or off. Switching it off releases every item no remaining reason applies to.
        /// </summary>
        public BanchosucksHoldRelease<T> SetActive(BanchosucksHoldReason reason, bool active)
        {
            bool wasActive = (Active & reason) != 0;

            if (active)
                Active |= reason;
            else
                Active &= ~reason;

            if (!wasActive || active)
                return new BanchosucksHoldRelease<T>(BanchosucksHoldReason.None, Array.Empty<T>());

            var released = new List<T>();

            for (int i = 0; i < held.Count; i++)
            {
                // an item taken for one reason stays held when another reason that applies to it began in the meantime
                if (BanchosucksNotificationPolicy.ReasonsFor(held[i].kind, Active) != BanchosucksHoldReason.None)
                    continue;

                released.Add(held[i].item);
                held.RemoveAt(i--);
            }

            if (held.Count == 0)
                Dropped = 0;

            return new BanchosucksHoldRelease<T>(reason, released);
        }
    }
}

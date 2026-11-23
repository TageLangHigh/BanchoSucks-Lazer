// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// Banchosucks: why a miss of the miss review happened, as far as the hit events of a score can tell.
    /// </summary>
    public enum BanchosucksMissReason
    {
        /// <summary>
        /// A key was pressed before the hit window, inside the miss window.
        /// </summary>
        TooEarly,

        /// <summary>
        /// The hit window ran out without a hit. The game judges a press after the window the same as no press, so
        /// "too late" and "not pressed" cannot be told apart here.
        /// </summary>
        NotHitInTime,

        /// <summary>
        /// The hit window ran out, and the closest click while the object was on screen landed beside it (osu! only).
        /// </summary>
        Aim,

        /// <summary>
        /// The hit window ran out although a click landed on the object: it came before the miss window or was held back by notelock (osu! only).
        /// </summary>
        ClickIgnored,

        /// <summary>
        /// Judged as a miss inside the hit window: the wrong key in osu!taiko, an object skipped by hitting a later one in osu!.
        /// </summary>
        InsideWindow,

        /// <summary>
        /// A combo break on an object without a hit window (slider tick, slider end, droplet, released hold).
        /// </summary>
        TickMissed,

        /// <summary>
        /// A miss the data says nothing more about (objects without a hit window, misses forced by another object).
        /// </summary>
        Missed,
    }

    /// <summary>
    /// Banchosucks: where the closest click of a missed object landed.
    /// </summary>
    /// <param name="Distance">Distance of the click from the centre of the object, in playfield units.</param>
    /// <param name="Radius">Radius of the object.</param>
    /// <param name="DistanceToNearestNeighbour">Distance of the same click from the centre of the nearer neighbouring object, if there is one.</param>
    public readonly record struct BanchosucksMissClick(float Distance, float Radius, float? DistanceToNearestNeighbour);

    /// <summary>
    /// Banchosucks: one line of the miss review.
    /// </summary>
    /// <param name="Time">Map time of the object (its start), used for display and as the replay position.</param>
    /// <param name="Reason">The reason.</param>
    /// <param name="TimeOffset">Offset of the judgement from the object in milliseconds (negative is early).</param>
    /// <param name="DistanceOutside">For <see cref="BanchosucksMissReason.Aim"/>: how far beside the object the click landed.</param>
    /// <param name="Count">How many combo breaks of one object this line stands for (ticks of the same slider are folded into one line).</param>
    public readonly record struct BanchosucksMiss(double Time, BanchosucksMissReason Reason, double TimeOffset, float? DistanceOutside = null, int Count = 1);

    /// <summary>
    /// Banchosucks: lets a ruleset tell the miss review where an object can be clicked. Implemented by osu!.
    /// </summary>
    public interface IBanchosucksHitAreaProvider
    {
        /// <summary>
        /// Centre and radius of the clickable area of <paramref name="hitObject"/> in the coordinates of <see cref="HitEvent.Position"/>, or null if it has none.
        /// </summary>
        (Vector2 centre, float radius)? GetHitArea(HitObject hitObject);
    }

    /// <summary>
    /// Banchosucks: turns the hit events of a score into the list of the miss review. Pure, no drawables.
    /// </summary>
    /// <remarks>
    /// What the hit events can and cannot say (all rulesets judge the same way here):
    /// a press inside the miss window but before the hit window gives a miss with a negative offset below the hit
    /// window, so "too early" is certain. Once the hit window has passed the object is judged as missed on its own, and
    /// a later press never reaches it, so "too late" and "not pressed" look the same. osu! additionally stores the
    /// closest click that reached the object while it was on screen (<see cref="HitEvent.Position"/>), which is used
    /// for "aim" only if that click was nearer to this object than to its neighbours, since it carries no time.
    /// </remarks>
    public static class BanchosucksMissClassifier
    {
        /// <summary>
        /// Classifies one judgement.
        /// </summary>
        /// <param name="result">The result. Anything that does not break the combo is not a miss and must not be passed.</param>
        /// <param name="timeOffset">Offset of the judgement from the object, negative is early.</param>
        /// <param name="hitWindow">Half width of the window in which the object can still be hit (the widest successful result), 0 if the object is not timed.</param>
        /// <param name="missWindow">Half width of the miss window, 0 if there is none.</param>
        /// <param name="click">The closest click, if the ruleset stores one.</param>
        public static BanchosucksMissReason Classify(HitResult result, double timeOffset, double hitWindow, double missWindow, BanchosucksMissClick? click = null)
        {
            if (!result.BreaksCombo())
                throw new ArgumentException($"{result} is not a miss.", nameof(result));

            if (hitWindow <= 0)
                return result == HitResult.Miss ? BanchosucksMissReason.Missed : BanchosucksMissReason.TickMissed;

            if (timeOffset > hitWindow)
            {
                if (click is BanchosucksMissClick c)
                {
                    if (c.Distance <= c.Radius)
                        return BanchosucksMissReason.ClickIgnored;

                    // The click has no time. Only blame the aim if it was meant for this object rather than one next to it.
                    if (c.DistanceToNearestNeighbour == null || c.Distance <= c.DistanceToNearestNeighbour)
                        return BanchosucksMissReason.Aim;
                }

                return BanchosucksMissReason.NotHitInTime;
            }

            if (timeOffset < -hitWindow)
            {
                // A press can only produce a judgement inside the miss window. Anything earlier was forced by another object.
                return timeOffset >= -Math.Max(missWindow, hitWindow) ? BanchosucksMissReason.TooEarly : BanchosucksMissReason.Missed;
            }

            return BanchosucksMissReason.InsideWindow;
        }

        /// <summary>
        /// Builds the miss review of a score from its hit events, ordered by map time.
        /// </summary>
        /// <param name="hitEvents">All hit events of the score.</param>
        /// <param name="hitAreas">The ruleset's hit areas, or null if it has none (no aim reasons then).</param>
        public static List<BanchosucksMiss> Analyse(IEnumerable<HitEvent> hitEvents, IBanchosucksHitAreaProvider? hitAreas = null)
        {
            var events = hitEvents.Where(e => e.HitObject != null).ToList();

            // nested object -> the object it belongs to, to fold the ticks of one slider into a single line
            var parents = new Dictionary<HitObject, HitObject>();

            foreach (var e in events)
            {
                foreach (var nested in e.HitObject.NestedHitObjects)
                    parents[nested] = e.HitObject;
            }

            // clickable objects in map order, to find the neighbours of a missed one
            var clickable = hitAreas == null
                ? new List<(HitObject hitObject, Vector2 centre)>()
                : events.Select(e => e.HitObject)
                        .Distinct()
                        .Where(h => getWindows(h).hitWindow > 0)
                        .Select(h => (hitObject: h, area: hitAreas.GetHitArea(h)))
                        .Where(t => t.area != null)
                        .Select(t => (t.hitObject, t.area!.Value.centre))
                        .OrderBy(t => t.hitObject.StartTime)
                        .ToList();

            var lines = new List<(HitObject hitObject, BanchosucksMiss miss)>();
            var tickLines = new Dictionary<HitObject, int>();

            foreach (var e in events.Where(e => e.Result.BreaksCombo()))
            {
                (double hitWindow, double missWindow) = getWindows(e.HitObject);

                BanchosucksMissClick? click = null;

                if (hitAreas != null && e.Position is Vector2 position && hitAreas.GetHitArea(e.HitObject) is (var centre, var radius))
                {
                    int index = clickable.FindIndex(t => t.hitObject == e.HitObject);
                    float? nearest = null;

                    foreach (int neighbour in new[] { index - 1, index + 1 })
                    {
                        if (index < 0 || neighbour < 0 || neighbour >= clickable.Count)
                            continue;

                        float distance = Vector2.Distance(position, clickable[neighbour].centre);
                        nearest = nearest == null ? distance : Math.Min(nearest.Value, distance);
                    }

                    click = new BanchosucksMissClick(Vector2.Distance(position, centre), radius, nearest);
                }

                var reason = Classify(e.Result, e.TimeOffset, hitWindow, missWindow, click);

                if (reason == BanchosucksMissReason.TickMissed && parents.TryGetValue(e.HitObject, out var parent))
                {
                    // one line per slider, however many of its ticks were lost
                    if (tickLines.TryGetValue(parent, out int line))
                    {
                        lines[line] = (lines[line].hitObject, lines[line].miss with { Count = lines[line].miss.Count + 1 });
                        continue;
                    }

                    tickLines[parent] = lines.Count;
                }

                lines.Add((e.HitObject, new BanchosucksMiss(
                    e.HitObject.StartTime,
                    reason,
                    e.TimeOffset,
                    reason == BanchosucksMissReason.Aim ? click!.Value.Distance - click.Value.Radius : null)));
            }

            var listed = new HashSet<HitObject>(lines.Select(l => l.hitObject));

            return lines
                   // An object whose parts are listed needs no line of its own (a fully missed slider in classic scoring: head and ticks say it all).
                   .Where(l => l.miss.Reason != BanchosucksMissReason.Missed || !l.hitObject.NestedHitObjects.Any(listed.Contains))
                   .Select(l => l.miss)
                   .OrderBy(m => m.Time)
                   .ToList();
        }

        private static (double hitWindow, double missWindow) getWindows(HitObject hitObject)
        {
            var windows = hitObject.HitWindows;

            if (windows == null)
                return (0, 0);

            double hitWindow = 0;
            double missWindow = 0;

            foreach ((HitResult result, double length) in windows.GetAllAvailableWindows())
            {
                if (result == HitResult.Miss)
                    missWindow = length;
                else
                    hitWindow = Math.Max(hitWindow, length);
            }

            return (hitWindow, missWindow);
        }

        /// <summary>
        /// The text of a line in English and German.
        /// </summary>
        /// <param name="miss">The line.</param>
        /// <param name="rulesetShortName">Short name of the score's ruleset ("osu", "taiko", "fruits", "mania"); decides the wording of <see cref="BanchosucksMissReason.InsideWindow"/>.</param>
        public static (string english, string german) Describe(BanchosucksMiss miss, string rulesetShortName)
        {
            switch (miss.Reason)
            {
                case BanchosucksMissReason.TooEarly:
                    return ($"Too early ({miss.TimeOffset:0} ms)", $"Zu früh ({miss.TimeOffset:0} ms)");

                case BanchosucksMissReason.NotHitInTime:
                    return ("Not hit in time (too late or no key press)", "Nicht rechtzeitig getroffen (zu spät oder nicht gedrückt)");

                case BanchosucksMissReason.Aim:
                    return ($"Aim: clicked {miss.DistanceOutside ?? 0:0} px beside the circle", $"Aim: {miss.DistanceOutside ?? 0:0} px neben dem Kreis geklickt");

                case BanchosucksMissReason.ClickIgnored:
                    return ("Click on the circle did not count (far too early or notelock)", "Klick auf den Kreis zählte nicht (viel zu früh oder Notelock)");

                case BanchosucksMissReason.InsideWindow:
                    switch (rulesetShortName)
                    {
                        case "taiko":
                            return ("Wrong key", "Falsche Taste");

                        case "osu":
                            return ("Skipped (a later object was hit first)", "Übersprungen (späteres Objekt zuerst getroffen)");

                        default:
                            return ("Pressed in time, but it did not count", "Rechtzeitig gedrückt, aber nicht gewertet");
                    }

                case BanchosucksMissReason.TickMissed:
                    return miss.Count > 1
                        ? ($"Combo break (let go or tick missed, {miss.Count}x)", $"Combo-Break (losgelassen oder Tick verpasst, {miss.Count}x)")
                        : ("Combo break (let go or tick missed)", "Combo-Break (losgelassen oder Tick verpasst)");

                default:
                    return ("Missed", "Verfehlt");
            }
        }
    }
}

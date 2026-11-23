// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Ranking;
using osu.Game.Users;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Practice
{
    /// <summary>
    /// Banchosucks: plays one section of a map in a loop, optionally slowed down. Restarts at the start point when the
    /// end point is reached, when everything is judged, or when the player fails; leaves only through the pause menu.
    /// </summary>
    /// <remarks>
    /// Deliberately a plain <see cref="Player"/> and not a <see cref="SubmittingPlayer"/>: no score token is requested,
    /// nothing is submitted, spectators get nothing, the beatmap's "last played" stays untouched and the clicker's
    /// Encore reward (which hangs on submission) cannot trigger. On top of that the local import is switched off, no
    /// replay is recorded and no results screen exists. Starting in the middle of a map works like the editor's test
    /// play (<see cref="Edit.GameplayTest.EditorPlayer"/>): everything before the start point counts as hit.
    /// </remarks>
    public partial class BanchosucksPracticePlayer : Player
    {
        /// <summary>
        /// How long the last judgement stays visible before the loop restarts after the final object.
        /// </summary>
        private const double completion_restart_delay = 600;

        public readonly BanchosucksPracticeSection Section;

        /// <summary>
        /// The map time the gameplay clock was set to at the start, or null when the run starts with the map.
        /// </summary>
        public double? JumpTime { get; private set; }

        /// <summary>
        /// Whether this run has asked the loader for the next one.
        /// </summary>
        public bool LoopRestartRequested { get; private set; }

        // Shown to others as plain "online": a practice loop is not a play, and nothing about it leaves the client.
        protected override UserActivity? InitialActivity => null;

        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly EmptyGameplayLeaderboardProvider leaderboardProvider = new EmptyGameplayLeaderboardProvider();

        // Tempo keeps the pitch (like Half Time without "adjust pitch"); it multiplies with rate mods the player has selected.
        private readonly BindableDouble tempo;

        private bool endsBeforeMapEnd;
        private OsuSpriteText hudText = null!;

        public BanchosucksPracticePlayer(BanchosucksPracticeSection section)
            : base(new PlayerConfiguration
            {
                ShowResults = false,
                AutomaticallySkipIntro = true,
            })
        {
            Section = section;
            tempo = new BindableDouble(section.Rate);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (!LoadedBeatmapSuccessfully)
                return;

            endsBeforeMapEnd = !Section.RunsToMapEnd(DrawableRuleset.Objects.Max(h => h.GetEndTime()));

            AddInternal(new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Margin = new MarginPadding { Left = 20, Bottom = 70 },
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 6,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.5f,
                    },
                    hudText = new OsuSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 10, Vertical = 5 },
                        Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                    },
                }
            });

            updateHudText();
        }

        private void updateHudText()
        {
            hudText.Text = ClickerStrings.Text("Practice {0} – {1} · {2}% · attempt {3}", "Übung {0} – {1} · {2} % · Versuch {3}",
                BanchosucksPracticeSection.FormatTime(Section.StartTime),
                BanchosucksPracticeSection.FormatTime(Section.EndTime),
                Section.RatePercent,
                Math.Max(1, Section.Attempts));
        }

        protected override GameplayClockContainer CreateGameplayClockContainer(WorkingBeatmap beatmap, double gameplayStart)
        {
            var master = new MasterGameplayClockContainer(beatmap, gameplayStart);

            // Same rule as the editor's test play: only jump if the start point is behind the normal start and the first object,
            // so a section that begins with the map keeps its normal lead-in.
            if (Section.StartTime > gameplayStart && !Section.StartsAtMapStart(DrawableRuleset.Objects.First().StartTime))
            {
                JumpTime = Section.StartTime;
                master.Reset(JumpTime);
            }

            master.AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Tempo, tempo);

            return master;
        }

        protected override void LoadAsyncComplete()
        {
            base.LoadAsyncComplete();

            if (!LoadedBeatmapSuccessfully || JumpTime == null)
                return;

            // Has to be installed before drawable hit objects run their first update (see EditorPlayer).
            preventMissOnPreviousHitObjects(JumpTime.Value);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!LoadedBeatmapSuccessfully)
                return;

            if (JumpTime != null)
                markPreviousObjectsHit(JumpTime.Value);

            ScoreProcessor.HasCompleted.BindValueChanged(completed =>
            {
                // Everything is judged: the section ran to the end of the map.
                if (completed.NewValue)
                    Scheduler.AddDelayed(restartLoop, completion_restart_delay);
            });
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            if (!LoadedBeatmapSuccessfully)
                return;

            Section.Attempts++;
            updateHudText();
        }

        protected override void Update()
        {
            base.Update();

            if (!LoadedBeatmapSuccessfully || !endsBeforeMapEnd)
                return;

            if (GameplayClockContainer.CurrentTime >= Section.EndTime)
                restartLoop();
        }

        private void restartLoop()
        {
            if (LoopRestartRequested || !this.IsCurrentScreen())
                return;

            LoopRestartRequested = true;

            // Goes back to the PlayerLoader, which builds the next run right away (quick restart).
            Restart(true);
        }

        /// <summary>
        /// A fail just starts the section again: no fail animation, no fail overlay (and so no "save replay" button).
        /// </summary>
        protected override void PerformFail() => Schedule(restartLoop);

        protected override void PrepareReplay()
        {
            // Practice runs are not recorded.
        }

        /// <summary>
        /// Practice runs never reach the local database.
        /// </summary>
        protected override Task ImportScore(Score score) => Task.CompletedTask;

        protected override ResultsScreen CreateResults(ScoreInfo score) => throw new NotSupportedException("Practice runs have no results screen.");

        #region Starting in the middle of a map (same approach as EditorPlayer)

        private void markPreviousObjectsHit(double cutoffTime)
        {
            foreach (var hitObject in enumerateHitObjects(DrawableRuleset.Objects, cutoffTime))
            {
                var judgement = hitObject.Judgement;

                // Fills the tallies of the processors so the run can complete at the end of the map and health starts full.
                var result = new JudgementResult(hitObject, judgement)
                {
                    Type = judgement.MaxResult,
                    GameplayRate = GameplayClockContainer.GetTrueGameplayRate(),
                };

                HealthProcessor.ApplyResult(result);
                ScoreProcessor.ApplyResult(result);
            }

            static IEnumerable<HitObject> enumerateHitObjects(IEnumerable<HitObject> hitObjects, double cutoffTime)
            {
                foreach (var hitObject in hitObjects)
                {
                    foreach (var nested in enumerateHitObjects(hitObject.NestedHitObjects, cutoffTime))
                    {
                        if (nested.GetEndTime() < cutoffTime)
                            yield return nested;
                    }

                    if (hitObject.GetEndTime() < cutoffTime)
                        yield return hitObject;
                }
            }
        }

        private void preventMissOnPreviousHitObjects(double cutoffTime)
        {
            void preventMiss(HitObject hitObject)
            {
                var drawableObject = DrawableRuleset.Playfield.HitObjectContainer
                                                    .AliveObjects
                                                    .SingleOrDefault(it => it.HitObject == hitObject);

                if (drawableObject != null)
                    preventMissOnDrawable(drawableObject);
            }

            void preventMissOnDrawable(DrawableHitObject drawableObject)
            {
                foreach (var nested in drawableObject.NestedHitObjects)
                    preventMissOnDrawable(nested);

                if (drawableObject.Entry != null && drawableObject.HitObject.GetEndTime() < cutoffTime)
                {
                    var result = drawableObject.CreateResult(drawableObject.HitObject.Judgement);
                    result.Type = result.Judgement.MaxResult;
                    drawableObject.Entry.Result = result;
                }
            }

            void removeListener()
            {
                if (!DrawableRuleset.Playfield.IsLoaded)
                {
                    Schedule(removeListener);
                    return;
                }

                DrawableRuleset.Playfield.HitObjectUsageBegan -= preventMiss;
            }

            DrawableRuleset.Playfield.HitObjectUsageBegan += preventMiss;

            Schedule(removeListener);
        }

        #endregion
    }
}

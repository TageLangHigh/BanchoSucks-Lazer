// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.Banchosucks;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// Banchosucks: the server status next to the toolbar clock: a dot for the connection to the lazer api and the round-trip
    /// time of the last status request; the tooltip adds the players online, open rooms and the daily challenge.
    /// </summary>
    /// <remarks>
    /// One small request (<c>banchosucks_status/summary</c>) every two minutes while the toolbar is on screen and the client
    /// is logged in; a click refreshes, but never sooner than a minute after the last request. Hidden toolbars are not
    /// updated by the framework, so nothing is asked during gameplay. Announcements still arrive through BanchoBot.
    /// </remarks>
    public partial class BanchosucksServerStatusIndicator : OsuClickableContainer
    {
        /// <summary>Milliseconds between two regular status requests.</summary>
        public const double REFRESH_INTERVAL = 120_000;

        private readonly BanchosucksRefreshGate gate = new BanchosucksRefreshGate(REFRESH_INTERVAL);

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? playInfo { get; set; }

        private IBindable<APIState> apiState = null!;
        private IBindable<LocalUserPlayingState>? playingState;

        private Box hoverBackground = null!;
        private Box flashBackground = null!;
        private Circle dot = null!;
        private OsuSpriteText label = null!;

        private bool refreshWanted;
        private double lastTextUpdate = double.MinValue;

        /// <summary>The last summary the server sent, or null.</summary>
        public BanchosucksStatusSummary? Summary { get; private set; }

        /// <summary>Round-trip time of the last answered status request.</summary>
        public TimeSpan? RoundTrip { get; private set; }

        /// <summary>What the dot currently shows.</summary>
        public BanchosucksConnectionState Connection { get; private set; }

        /// <summary>The text next to the dot (tests).</summary>
        public string LabelText => label.Text.ToString();

        /// <summary>The clock the maintenance countdown runs on (tests replace it).</summary>
        public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

        public BanchosucksServerStatusIndicator()
        {
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Padding = new MarginPadding(ToolbarButton.PADDING),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 6,
                            CornerExponent = 3f,
                            Children = new Drawable[]
                            {
                                hoverBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = OsuColour.Gray(80).Opacity(180),
                                    Blending = BlendingParameters.Additive,
                                    Alpha = 0,
                                },
                                flashBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    Colour = Color4.White.Opacity(100),
                                    Blending = BlendingParameters.Additive,
                                },
                            }
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6),
                            Padding = new MarginPadding { Horizontal = 8 },
                            Children = new Drawable[]
                            {
                                dot = new Circle
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(8),
                                },
                                label = new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold, fixedWidth: true),
                                },
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            playingState = playInfo?.PlayingState.GetBoundCopy();

            apiState = api.State.GetBoundCopy();
            apiState.BindValueChanged(_ => Schedule(updateDisplay), true);
        }

        protected override void Update()
        {
            base.Update();

            bool playing = playingState != null && playingState.Value != LocalUserPlayingState.NotPlaying;

            // the client sends no api requests while logged out, and nothing is asked during gameplay
            if (apiState.Value == APIState.Online && !playing && gate.ShouldRefresh(Time.Current, refreshWanted))
                refresh();

            // the countdown and "x seconds ago" move on without a request
            if (Time.Current - lastTextUpdate >= 1000 || Time.Current < lastTextUpdate)
                updateDisplay();
        }

        private void refresh()
        {
            refreshWanted = false;
            gate.Started(Time.Current);

            var request = new GetBanchosucksStatusSummaryRequest();

            request.Success += summary => Schedule(() =>
            {
                gate.Succeeded(Time.Current);
                Summary = summary;
                RoundTrip = request.RoundTrip;
                updateDisplay();
            });

            request.Failure += _ => Schedule(() =>
            {
                gate.Failed();
                updateDisplay();
            });

            api.Queue(request);
        }

        private void updateDisplay()
        {
            lastTextUpdate = Time.Current;

            Connection = BanchosucksServerStatus.Connection(apiState.Value, gate.LastFailed);

            var maintenance = Connection == BanchosucksConnectionState.Offline ? null : BanchosucksServerStatus.MaintenanceIn(Summary, Now());
            int? milliseconds = BanchosucksServerStatus.Milliseconds(RoundTrip);

            switch (Connection)
            {
                case BanchosucksConnectionState.Online:
                    dot.Colour = maintenance != null ? colours.Yellow : colours.Green;
                    label.Text = milliseconds != null ? $"{milliseconds} ms" : @"online";
                    break;

                case BanchosucksConnectionState.Connecting:
                    dot.Colour = colours.Yellow;
                    label.Text = ClickerStrings.Pick("connecting", "verbinde");
                    break;

                case BanchosucksConnectionState.Trouble:
                    dot.Colour = colours.Orange1;
                    label.Text = ClickerStrings.Pick("unstable", "gestört");
                    break;

                default:
                    dot.Colour = colours.Gray7;
                    label.Text = @"offline";
                    break;
            }

            if (maintenance != null)
            {
                label.Text = maintenance.Value > TimeSpan.Zero
                    ? ClickerStrings.Pick($"restart {BanchosucksServerStatus.FormatCountdown(maintenance.Value)}", $"Neustart {BanchosucksServerStatus.FormatCountdown(maintenance.Value)}")
                    : ClickerStrings.Pick("restart now", "Neustart jetzt");
            }

            TooltipText = describe(maintenance, milliseconds);
        }

        private string describe(TimeSpan? maintenance, int? milliseconds)
        {
            var lines = new List<string>();

            switch (Connection)
            {
                case BanchosucksConnectionState.Online:
                    lines.Add(ClickerStrings.Pick("Banchosucks Lazer: connected", "Banchosucks Lazer: verbunden"));
                    break;

                case BanchosucksConnectionState.Connecting:
                    lines.Add(ClickerStrings.Pick("Banchosucks Lazer: connecting...", "Banchosucks Lazer: Verbindung wird aufgebaut..."));
                    break;

                case BanchosucksConnectionState.Trouble:
                    lines.Add(apiState.Value == APIState.Online
                        ? ClickerStrings.Pick("Banchosucks Lazer: connected, but the last status request failed", "Banchosucks Lazer: verbunden, aber die letzte Statusabfrage schlug fehl")
                        : ClickerStrings.Pick("Banchosucks Lazer: the server does not answer, trying again", "Banchosucks Lazer: der Server antwortet nicht, neuer Versuch läuft"));
                    break;

                default:
                    lines.Add(ClickerStrings.Pick("Banchosucks Lazer: not logged in", "Banchosucks Lazer: nicht angemeldet"));
                    return string.Join('\n', lines);
            }

            if (maintenance != null)
            {
                lines.Add(maintenance.Value > TimeSpan.Zero
                    ? ClickerStrings.Pick($"Planned restart in {BanchosucksServerStatus.FormatCountdown(maintenance.Value)}", $"Geplanter Neustart in {BanchosucksServerStatus.FormatCountdown(maintenance.Value)}")
                    : ClickerStrings.Pick("Planned restart: now", "Geplanter Neustart: jetzt"));

                if (!string.IsNullOrWhiteSpace(Summary?.MaintenanceMessage))
                    lines.Add(Summary.MaintenanceMessage.Trim());
            }

            if (milliseconds != null)
                lines.Add(ClickerStrings.Pick($"Response time: {milliseconds} ms (last status request)", $"Antwortzeit: {milliseconds} ms (letzte Statusabfrage)"));

            if (Summary != null)
            {
                lines.Add(ClickerStrings.Pick($"Players online (lazer): {Summary.LazerOnline}", $"Spieler online (Lazer): {Summary.LazerOnline}"));
                lines.Add(ClickerStrings.Pick($"Open multiplayer rooms: {Summary.OpenRooms}", $"Offene Multiplayer-Räume: {Summary.OpenRooms}"));

                if (!string.IsNullOrWhiteSpace(Summary.DailyChallenge))
                    lines.Add(ClickerStrings.Pick($"Daily challenge: {Summary.DailyChallenge}", $"Daily Challenge: {Summary.DailyChallenge}"));
            }

            if (gate.Age(Time.Current) is double age)
            {
                int seconds = (int)(age / 1000);

                lines.Add(seconds < 60
                    ? ClickerStrings.Pick($"As of {seconds} s ago. Click to refresh.", $"Stand: vor {seconds} s. Klick aktualisiert.")
                    : ClickerStrings.Pick($"As of {seconds / 60} min ago. Click to refresh.", $"Stand: vor {seconds / 60} min. Klick aktualisiert."));
            }

            return string.Join('\n', lines);
        }

        protected override bool OnClick(ClickEvent e)
        {
            flashBackground.FadeOutFromOne(800, Easing.OutQuint);

            // asked for by the player: skips the two-minute rhythm, not the one-minute floor
            refreshWanted = true;

            return base.OnClick(e);
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverBackground.FadeIn(200);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverBackground.FadeOut(200);
            base.OnHoverLost(e);
        }

        protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverClickSounds(sampleSet);
    }
}

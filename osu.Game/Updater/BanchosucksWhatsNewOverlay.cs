// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Updater
{
    /// <summary>
    /// Banchosucks: "what's new". Shows the release notes of the running version once after an update, in the
    /// player's language, as a dialog that closes with its button, the X, Escape or a click next to it.
    /// </summary>
    /// <remarks>
    /// <see cref="OsuGame"/> creates it through <see cref="CreateIfDue"/> when the main menu comes up for the first time
    /// in a session. Which notes are due is decided by <see cref="BanchosucksReleaseNotes.ShouldShow"/>.
    /// </remarks>
    public partial class BanchosucksWhatsNewOverlay : OsuFocusedOverlayContainer
    {
        private const double enter_duration = 500;
        private const double exit_duration = 200;

        public readonly string Version;
        public readonly BanchosucksReleaseNotes Notes;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private OsuTextFlowContainer text = null!;
        private IBindable<LocalisationParameters>? localisationParameters;

        public BanchosucksWhatsNewOverlay(string version, BanchosucksReleaseNotes notes)
        {
            Version = version;
            Notes = notes;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.5f, 0.75f);

            Masking = true;
            CornerRadius = 10;
        }

        /// <summary>
        /// Decides at the first main menu of a session whether notes are shown, and remembers the decision
        /// in <see cref="OsuSetting.BanchosucksWhatsNewShownVersion"/> so that it is made once per version.
        /// </summary>
        /// <param name="config">The game configuration.</param>
        /// <param name="previousVersion">The version stored by the previous start; empty on a fresh install.</param>
        /// <param name="currentVersion">The running version.</param>
        /// <returns>The dialog to show, or null.</returns>
        public static BanchosucksWhatsNewOverlay? CreateIfDue(OsuConfigManager config, string? previousVersion, string currentVersion)
        {
            string current = BanchosucksReleaseNotes.NormaliseVersion(currentVersion);

            if (current.Length == 0)
                return null;

            bool due = BanchosucksReleaseNotes.ShouldShow(previousVersion, config.Get<string>(OsuSetting.BanchosucksWhatsNewShownVersion), currentVersion);

            // also on a fresh install and when there is no file: this version is dealt with
            config.SetValue(OsuSetting.BanchosucksWhatsNewShownVersion, current);

            if (!due)
                return null;

            var notes = BanchosucksReleaseNotes.LoadEmbedded(current);

            return notes == null ? null : new BanchosucksWhatsNewOverlay(current, notes);
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, LocalisationManager? localisation)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    Colour = colours.GreySeaFoamDark,
                    RelativeSizeAxes = Axes.Both,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = ClickerStrings.Text("What's new in {0}", "Was ist neu in {0}", Version),
                                        Font = OsuFont.GetFont(size: 28),
                                        Padding = new MarginPadding { Vertical = 12 },
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Icon = FontAwesome.Solid.Times,
                                        Colour = colours.GreySeaFoamDarker,
                                        Scale = new Vector2(0.8f),
                                        X = -10,
                                        Action = Hide,
                                    },
                                },
                            },
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colours.GreySeaFoamDarker,
                                    },
                                    new OsuScrollContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Child = text = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 16))
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Padding = new MarginPadding { Horizontal = 24, Vertical = 16 },
                                            ParagraphSpacing = 0.6f,
                                        },
                                    },
                                },
                            },
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding(12),
                                Child = new RoundedButton
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Width = 220,
                                    Text = ClickerStrings.Text("Close", "Schließen"),
                                    Action = Hide,
                                },
                            },
                        },
                    },
                },
            };

            if (localisation != null)
            {
                localisationParameters = localisation.CurrentParameters.GetBoundCopy();
                localisationParameters.BindValueChanged(parameters => showNotes(parameters.NewValue.Store?.EffectiveCulture.TwoLetterISOLanguageName == "de"), true);
            }
            else
                showNotes(false);
        }

        /// <summary>
        /// The language the notes are currently drawn in.
        /// </summary>
        public bool ShowingGerman { get; private set; }

        private void showNotes(bool german)
        {
            ShowingGerman = german;
            text.Clear();

            bool first = true;

            foreach (var section in Notes.For(german))
            {
                if (section.Title.Length > 0)
                {
                    if (first)
                        text.AddText(section.Title, t => t.Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold));
                    else
                        text.AddParagraph(section.Title, t => t.Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold));

                    first = false;
                }

                foreach (var entry in section.Entries)
                {
                    string line = entry.Bullet ? $"•  {entry.Text}" : entry.Text;

                    if (first)
                        text.AddText(line);
                    else
                        text.AddParagraph(line);

                    first = false;
                }
            }
        }

        protected override void PopIn()
        {
            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(0.9f).Then().ScaleTo(1f, enter_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();

            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.9f, exit_duration);
        }
    }
}

// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.IO.Network;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;
using osu.Game.Screens.Backgrounds;
using SixLabors.ImageSharp;
using Image = SixLabors.ImageSharp.Image;
using Rgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;
using osuTK;

namespace osu.Game.Screens.Menu
{
    public partial class BackgroundEditorOverlay : OsuFocusedOverlayContainer, INamedOverlayComponent
    {
        public IconUsage Icon => FontAwesome.Solid.Image;
        public LocalisableString Title => UserInterfaceStrings.BackgroundEditor;
        public LocalisableString Description => UserInterfaceStrings.BackgroundEditor;

        private const float header_height = 40;
        private const double carousel_interval = 3000;

        /// <summary>
        /// The namespace the bundled game textures live in, relative to the game's resource store.
        /// </summary>
        private const string bundled_texture_namespace = @"Textures";

        private readonly FillFlowContainer optionsFlow;
        private readonly FormToggleGroup<BackgroundSource> sourceGroup;
        private readonly FormToggleGroup<SeasonalBackgroundMode> seasonalGroup;

        /// <summary>
        /// Disabled while an export is being written, which is what keeps a second export from being started.
        /// </summary>
        private IconButton exportButton = null!;

        private BackgroundOption? skinOption;
        private BackgroundOption? beatmapOption;
        private BackgroundOption? seasonalRandomOption;
        private readonly List<BackgroundOption> pinnedOptions = new List<BackgroundOption>();

        /// <summary>
        /// Keeps one reference to every seasonal background texture to prevent the LargeTextureStore from disposing it.
        /// </summary>
        private readonly List<Texture> seasonalPins = new List<Texture>();

        /// <summary>
        /// Guards <see cref="seasonalPins"/>, which is also written by the seasonal thumbnail batch thread.
        /// </summary>
        private readonly Lock seasonalPinsLock = new Lock();

        /// <summary>
        /// Set on disposal, after which the thumbnail batch may no longer pin any texture.
        /// </summary>
        private bool seasonalPinsReleased;

        /// <summary>
        /// Seasonal backgrounds which were successfully loaded, used to drive the random entry's carousel.
        /// </summary>
        private readonly List<string> carouselUrls = new List<string>();

        private int carouselIndex;

        private bool optionsBuilt;

        /// <summary>
        /// Incremented whenever the available seasonal backgrounds change, so that results of an
        /// outdated background texture load are discarded rather than written into new options.
        /// </summary>
        private int seasonalLoadGeneration;

        private Bindable<BackgroundSource> source = null!;
        private Bindable<SeasonalBackgroundMode> seasonalMode = null!;
        private Bindable<bool> pinnedSeasonal = null!;
        private Bindable<int> pinnedSeasonalIndex = null!;
        private Bindable<APISeasonalBackgrounds> seasonalBackgrounds = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private LargeTextureStore textures { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SessionStatics sessionStatics { get; set; } = null!;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private Storage exportStorage = null!;

        /// <summary>
        /// The default background screen of the main menu, used to resolve the currently displayed
        /// background for export. Set by the owning screen.
        /// </summary>
        private BackgroundScreenDefault? defaultScreen;

        public BackgroundEditorOverlay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Masking = true;

            State.Value = Visibility.Hidden;

            const float corner_radius = 7f;

            CornerRadius = corner_radius;

            // Hack to hide the bottom edge corner radius off-screen.
            Margin = new MarginPadding { Bottom = -corner_radius };
            Padding = new MarginPadding { Bottom = corner_radius };

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background5,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Children = new[]
                            {
                                createHeader(),
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding(15),
                                    Child = new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
                                        ColumnDimensions =
                                        [
                                            new Dimension(),
                                            new Dimension(GridSizeMode.AutoSize),
                                        ],
                                        Content = new Drawable[][]
                                        {
                                            [
                                                // a scroll container cannot auto-size along its scroll axis,
                                                // so the height is fixed to the option height plus the scrollbar.
                                                new OsuScrollContainer(Direction.Horizontal)
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    ScrollbarOverlapsContent = false,
                                                    Height = BackgroundOption.OPTION_HEIGHT + 24,
                                                    Child = optionsFlow = new FillFlowContainer
                                                    {
                                                        AutoSizeAxes = Axes.Both,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(8),
                                                    },
                                                },
                                                // Settings area.
                                                new FillFlowContainer
                                                {
                                                    Width = 380,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(8),
                                                    Padding = new MarginPadding { Left = 8 },
                                                    Children = new Drawable[]
                                                    {
                                                        sourceGroup = new FormToggleGroup<BackgroundSource>
                                                        {
                                                            Caption = UserInterfaceStrings.BackgroundSource,
                                                            Items = new[]
                                                            {
                                                                createOption(BackgroundSource.Skin, FontAwesome.Solid.PaintBrush),
                                                                createOption(BackgroundSource.Beatmap, FontAwesome.Solid.Music),
                                                                createOption(BackgroundSource.BeatmapWithStoryboard, FontAwesome.Solid.Film),
                                                            },
                                                        },
                                                        seasonalGroup = new FormToggleGroup<SeasonalBackgroundMode>
                                                        {
                                                            Caption = UserInterfaceStrings.SeasonalBackgrounds,
                                                            Items = new[]
                                                            {
                                                                createOption(SeasonalBackgroundMode.Always, FontAwesome.Solid.CalendarAlt),
                                                                createOption(SeasonalBackgroundMode.Sometimes, FontAwesome.Solid.CalendarDay),
                                                                createOption(SeasonalBackgroundMode.Never, FontAwesome.Solid.CalendarTimes),
                                                            },
                                                        },
                                                    },
                                                },
                                            ],
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            // start off-screen below the bottom edge; PopIn slides it up.
            Y = 600;
        }

        private Drawable createHeader() => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = header_height,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background3,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions =
                    [
                        new Dimension(GridSizeMode.Absolute, 50),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    ],
                    Content = new Drawable[][]
                    {
                        [
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Image,
                                Size = new Vector2(20),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = UserInterfaceStrings.BackgroundEditor,
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                Margin = new MarginPadding { Bottom = 2 },
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(4),
                                Padding = new MarginPadding { Right = 4 },
                                Children = new Drawable[]
                                {
                                    exportButton = new IconButton
                                    {
                                        Icon = FontAwesome.Solid.Download,
                                        Size = new Vector2(32),
                                        IconScale = new Vector2(0.8f),
                                        TooltipText = UserInterfaceStrings.ExportBackground,
                                        Action = exportBackground,
                                    },
                                    new IconButton
                                    {
                                        Icon = FontAwesome.Solid.SignOutAlt,
                                        Size = new Vector2(32),
                                        IconScale = new Vector2(0.8f),
                                        TooltipText = UserInterfaceStrings.SaveAndExit,
                                        Action = Hide,
                                    },
                                },
                            },
                        ],
                    },
                },
            },
        };

        /// <summary>
        /// Creates an option which is presented as an icon alone, so that the text it stands for is only
        /// shown in its tooltip.
        /// </summary>
        private static FormToggleGroupItem<T> createOption<T>(T value, IconUsage icon) => new FormToggleGroupItem<T>
        {
            Value = value,
            Icon = icon,
            TooltipText = value.GetLocalisableDescription(),
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            source = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource);
            seasonalMode = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode);
            pinnedSeasonal = config.GetBindable<bool>(OsuSetting.PinnedSeasonalBackground);
            pinnedSeasonalIndex = config.GetBindable<int>(OsuSetting.PinnedSeasonalBackgroundIndex);
            seasonalBackgrounds = sessionStatics.GetBindable<APISeasonalBackgrounds>(Static.SeasonalBackgrounds);

            exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");

            // these groups are the same as the dropdowns in the settings sidebar; keep the same bindables
            // so changes apply live and are visible in both places.
            sourceGroup.Current = source;
            seasonalGroup.Current = seasonalMode;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            source.BindValueChanged(_ => Schedule(refreshSelection), true);
            seasonalMode.BindValueChanged(_ => Schedule(refreshSelection));
            pinnedSeasonal.BindValueChanged(_ => Schedule(refreshSelection));
            pinnedSeasonalIndex.BindValueChanged(_ => Schedule(refreshSelection));

            // the previews may change while the editor is closed; only the affected thumbnail is refreshed.
            beatmap.BindValueChanged(_ => Schedule(refreshBeatmapThumbnail));
            skinManager.CurrentSkin.BindValueChanged(_ => Schedule(refreshSkinThumbnail));

            seasonalBackgrounds.BindValueChanged(_ => Schedule(() =>
            {
                if (!optionsBuilt)
                    return;

                rebuildOptions();
                refreshSelection();
            }), true);

            // rotates the random seasonal entry's preview through every available image.
            Scheduler.AddDelayed(carouselTick, carousel_interval);
        }

        private void carouselTick()
        {
            if (State.Value == Visibility.Visible && carouselUrls.Count > 1)
            {
                carouselIndex = (carouselIndex + 1) % carouselUrls.Count;
                showCarouselImage(carouselUrls[carouselIndex]);
            }

            Scheduler.AddDelayed(carouselTick, carousel_interval);
        }

        private void showCarouselImage(string url) => seasonalRandomOption?.SetTexture(textures.Get(url), url);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (!isDisposing)
                return;

            // stops the thumbnail batch from resolving any further images. images it may already hold are
            // released by the batch thread itself, as it can no longer hand them to the scheduler.
            Interlocked.Increment(ref seasonalLoadGeneration);

            lock (seasonalPinsLock)
            {
                seasonalPinsReleased = true;
                releaseSeasonalPinsLocked();
            }
        }

        private void releaseSeasonalPins()
        {
            lock (seasonalPinsLock)
                releaseSeasonalPinsLocked();
        }

        private void releaseSeasonalPinsLocked()
        {
            foreach (var pin in seasonalPins)
                pin.Dispose();

            seasonalPins.Clear();
        }

        /// <summary>
        /// Takes ownership of a seasonal background texture obtained by the thumbnail batch.
        /// </summary>
        /// <returns>
        /// Whether the texture was retained. <c>false</c> means the overlay has been disposed, and the caller
        /// has to release the texture itself.
        /// </returns>
        private bool tryPinSeasonalTexture(Texture texture)
        {
            lock (seasonalPinsLock)
            {
                if (seasonalPinsReleased)
                    return false;

                seasonalPins.Add(texture);
                return true;
            }
        }

        /// <summary>
        /// Releases a single pinned texture, if it is still being held.
        /// </summary>
        private void releaseSeasonalTexture(Texture texture)
        {
            lock (seasonalPinsLock)
            {
                if (!seasonalPins.Remove(texture))
                    return;
            }

            texture.Dispose();
        }

        /// <summary>
        /// Provide the default background screen, used for resolving the currently displayed background on export.
        /// </summary>
        public void SetDefaultScreen(BackgroundScreenDefault? screen) => defaultScreen = screen;

        protected override void PopIn()
        {
            // built once on first open; subsequent openings reuse the existing options (and their thumbnails).
            if (!optionsBuilt)
            {
                rebuildOptions();
                optionsBuilt = true;
            }

            // the selection (and the account state it depends on) may have changed while the editor was closed.
            refreshSelection();

            this.MoveToY(0, 400, Easing.OutQuint);
            this.FadeIn(400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.MoveToY(DrawHeight, 400, Easing.InSine);
            this.FadeOut(400, Easing.OutQuint);
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            if (e.Action == GlobalAction.Select)
            {
                // exiting the editor saves changes implicitly, as all changes apply live.
                Hide();
                return true;
            }

            // back is handled by the base class (also hides the overlay).
            return base.OnPressed(e);
        }

        private bool seasonalDataAvailable => seasonalBackgrounds.Value?.Backgrounds?.Any() == true;

        private bool seasonalDisplayed => seasonalMode.Value != SeasonalBackgroundMode.Never;

        private void rebuildOptions()
        {
            // any seasonal texture load still in flight belongs to the options about to be discarded.
            Interlocked.Increment(ref seasonalLoadGeneration);

            optionsFlow.Clear();
            pinnedOptions.Clear();
            carouselUrls.Clear();
            carouselIndex = 0;

            // released after the options (and therefore the sprites referencing those textures) are gone.
            releaseSeasonalPins();

            skinOption = new BackgroundOption(BackgroundOptionType.Skin, getSkinTexture(), FontAwesome.Solid.PaintBrush)
            {
                TooltipText = UserInterfaceStrings.SkinBackground,
                Action = () =>
                {
                    if (source.Value != BackgroundSource.Skin)
                        source.Value = BackgroundSource.Skin;

                    // a pinned background takes priority over the source, so it has to be released for the
                    // requested source to actually take effect.
                    pinnedSeasonal.Value = false;
                    seasonalMode.Value = SeasonalBackgroundMode.Never;
                },
            };
            optionsFlow.Add(skinOption);

            beatmapOption = new BackgroundOption(BackgroundOptionType.Beatmap, beatmap.Value.GetBackground(), FontAwesome.Solid.Music)
            {
                TooltipText = UserInterfaceStrings.BeatmapBackground,
                Action = () =>
                {
                    if (source.Value != BackgroundSource.Beatmap && source.Value != BackgroundSource.BeatmapWithStoryboard)
                        source.Value = BackgroundSource.Beatmap;

                    pinnedSeasonal.Value = false;
                    seasonalMode.Value = SeasonalBackgroundMode.Never;
                },
            };
            optionsFlow.Add(beatmapOption);

            // seasonal random entry: corresponds to setting "seasonal backgrounds" to "always".
            // its preview cycles through every available seasonal background.
            seasonalRandomOption = new BackgroundOption(BackgroundOptionType.SeasonalRandom, badgeIcon: FontAwesome.Solid.Random)
            {
                TooltipText = seasonalDataAvailable ? UserInterfaceStrings.RandomSeasonalBackground : UserInterfaceStrings.Unavailable,
                Enabled = { Value = seasonalDataAvailable },
                Action = () =>
                {
                    pinnedSeasonal.Value = false;
                    seasonalMode.Value = SeasonalBackgroundMode.Always;
                },
            };
            optionsFlow.Add(seasonalRandomOption);

            if (seasonalDataAvailable)
            {
                int count = seasonalBackgrounds.Value!.Backgrounds!.Count;

                for (int i = 0; i < count; i++)
                {
                    int index = i;

                    var option = new BackgroundOption(BackgroundOptionType.SeasonalPinned)
                    {
                        Index = index,
                        TooltipText = UserInterfaceStrings.PinnedSeasonalBackgroundItem(index + 1),
                        Action = () =>
                        {
                            pinnedSeasonal.Value = true;
                            pinnedSeasonalIndex.Value = index;
                            seasonalMode.Value = SeasonalBackgroundMode.Always;
                        },
                    };

                    pinnedOptions.Add(option);
                    optionsFlow.Add(option);
                }
            }

            beginSeasonalTextureLoad();
        }

        private Texture getSkinTexture() => skinManager.CurrentSkin.Value.GetTexture(@"menu-background")
                                            ?? textures.Get(@"Menu/menu-background-1");

        private void refreshSkinThumbnail() => skinOption?.SetTexture(getSkinTexture());

        private void refreshBeatmapThumbnail() => beatmapOption?.SetTexture(beatmap.Value.GetBackground());

        /// <summary>
        /// Fetches the thumbnails of all available seasonal backgrounds and hands them to the options
        /// as they arrive, so that the previews fill in progressively rather than all at once.
        /// </summary>
        private void beginSeasonalTextureLoad()
        {
            var backgrounds = seasonalBackgrounds.Value?.Backgrounds;

            if (backgrounds == null || backgrounds.Count == 0)
                return;

            int generation = Interlocked.Increment(ref seasonalLoadGeneration);
            string[] urls = backgrounds.Select(b => b.Url).ToArray();

            seasonalRandomOption?.BeginAsyncLoad();

            foreach (var option in pinnedOptions)
                option.BeginAsyncLoad();

            // the texture store blocks internally when a lookup for the same name is already in flight
            // (which happens whenever the menu background is downloading the very same image). Blocking
            // is only permitted from a non-thread-pool thread, so the whole batch gets a dedicated thread.
            Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < urls.Length; i++)
                {
                    if (generation != Volatile.Read(ref seasonalLoadGeneration))
                        return;

                    Texture? pin;

                    try
                    {
                        pin = textures.Get(urls[i]);
                    }
                    catch
                    {
                        // a single failing image should not prevent the rest from being displayed.
                        pin = null;
                    }

                    if (pin == null)
                        continue;

                    // the texture is pinned on this thread rather than in the callback below, so that it is
                    // still released when the overlay is disposed before that callback gets to run.
                    if (!tryPinSeasonalTexture(pin))
                    {
                        pin.Dispose();
                        return;
                    }

                    string url = urls[i];
                    int index = i;

                    Scheduler.Add(() =>
                    {
                        if (generation != Volatile.Read(ref seasonalLoadGeneration))
                        {
                            // the options this image belongs to have been discarded, so nothing will display it.
                            releaseSeasonalTexture(pin);
                            return;
                        }

                        carouselUrls.Add(url);

                        if (carouselUrls.Count == 1)
                        {
                            carouselIndex = 0;
                            showCarouselImage(url);
                        }

                        if (index < pinnedOptions.Count)
                            pinnedOptions[index].SetTexture(textures.Get(url), url);
                    });
                }

                Scheduler.Add(() =>
                {
                    if (generation != Volatile.Read(ref seasonalLoadGeneration))
                        return;

                    // whatever is still spinning has either failed or is not going to arrive.
                    seasonalRandomOption?.EndAsyncLoad();

                    foreach (var option in pinnedOptions)
                        option.EndAsyncLoad();
                });
            }, TaskCreationOptions.LongRunning);
        }

        private void refreshSelection()
        {
            int availableCount = seasonalBackgrounds.Value?.Backgrounds?.Count ?? 0;
            bool pinnedIndexValid = pinnedSeasonal.Value && pinnedSeasonalIndex.Value >= 0 && pinnedSeasonalIndex.Value < availableCount;

            foreach (var option in optionsFlow.Children.OfType<BackgroundOption>())
            {
                option.Selected = option.Type switch
                {
                    BackgroundOptionType.Skin => source.Value == BackgroundSource.Skin,
                    BackgroundOptionType.Beatmap => source.Value is BackgroundSource.Beatmap or BackgroundSource.BeatmapWithStoryboard,
                    BackgroundOptionType.SeasonalRandom => seasonalDisplayed && !pinnedIndexValid,
                    BackgroundOptionType.SeasonalPinned => seasonalDisplayed && pinnedIndexValid && pinnedSeasonalIndex.Value == option.Index,
                    _ => false,
                };
            }
        }

        private void exportBackground()
        {
            exportButton.Enabled.Value = false;

            // resolved on the update thread so that the export always matches what is currently on screen, even if
            // the selection (or the beatmap / skin) changes while the file is being written.
            Background? displayed = defaultScreen?.CurrentBackground;

            // only a seasonal image which was actually retrieved from its remote URL can be downloaded again;
            // anything else on screen has to be read from a local source.
            string? seasonalUrl = displayed is SeasonalBackground seasonal
                                  && seasonal.LoadedUrl != null
                                  && TrustedDomainOnlineStore.IsAllowedUrl(seasonal.LoadedUrl, config)
                ? seasonal.LoadedUrl
                : null;

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = UserInterfaceStrings.ExportBackground,
                CompletionText = UserInterfaceStrings.ExportBackground,
            };

            notifications.Post(notification);

            // the editor may be hidden (or disposed) while the export runs, at which point its own scheduler
            // stops running; the update thread's scheduler always does, so the notification always completes.
            void post(Action action) => gameHost.UpdateThread.Scheduler.Add(action);

            void onCompletion(string filename)
            {
                notification.CompletionClickAction = () =>
                {
                    exportStorage.PresentFileExternally(filename);
                    return true;
                };

                notification.CompletionText = UserInterfaceStrings.BackgroundExported(filename);
                notification.Progress = 1;
                notification.State = ProgressNotificationState.Completed;
            }

            void onFailure()
            {
                notification.Text = UserInterfaceStrings.BackgroundExportFailed;
                notification.State = ProgressNotificationState.Cancelled;
            }

            CancellationToken cancellationToken = notification.CancellationToken;

            // all exceptions are handled inside, so the task itself can be safely discarded.
            _ = Task.Run(async () =>
            {
                try
                {
                    using Stream? stream = seasonalUrl != null
                        ? await downloadImageAsync(seasonalUrl, cancellationToken).ConfigureAwait(false)
                        : getLocalBackgroundStream(displayed);

                    if (stream == null)
                    {
                        post(onFailure);
                        return;
                    }

                    using (stream)
                    using (var image = Image.Load<Rgba32>(stream))
                    {
                        // the user has already dismissed the notification and given up on the export.
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        string filename = createExportFilename();
                        string temporaryFilename = $"{filename}.tmp";

                        try
                        {
                            // encode next to the destination first, so that a failed export cannot leave a partial image behind.
                            using (var outputStream = exportStorage.GetStream(temporaryFilename, FileAccess.Write, FileMode.Create))
                                await image.SaveAsPngAsync(outputStream).ConfigureAwait(false);

                            if (cancellationToken.IsCancellationRequested)
                                return;

                            exportStorage.Move(temporaryFilename, filename);
                        }
                        finally
                        {
                            // a canceled or failed export must not leave the partially written image behind.
                            // this is a no-op once it has been moved into place.
                            exportStorage.Delete(temporaryFilename);
                        }

                        post(() => onCompletion(filename));
                    }
                }
                catch (Exception)
                {
                    post(onFailure);
                }
                finally
                {
                    post(() => exportButton.Enabled.Value = true);
                }
            }, CancellationToken.None);
        }

        /// <summary>
        /// A filename which does not clash with an existing export.
        /// </summary>
        private string createExportFilename()
        {
            string timestamp = DateTime.Now.ToString(@"yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string filename = $@"background_{timestamp}.png";

            for (int duplicate = 2; exportStorage.Exists(filename); duplicate++)
                filename = $@"background_{timestamp}_{duplicate}.png";

            return filename;
        }

        private static async Task<Stream?> downloadImageAsync(string url, CancellationToken cancellationToken)
        {
            var request = new WebRequest(url);

            // aborts the download when the export is cancelled while it is in progress.
            await request.PerformAsync(cancellationToken).ConfigureAwait(false);

            byte[]? data = request.GetResponseData();

            return data == null ? null : new MemoryStream(data);
        }

        /// <summary>
        /// Resolve the image on screen to a stream which can be read off the update thread.
        /// </summary>
        private Stream? getLocalBackgroundStream(Background? displayed)
        {
            switch (displayed)
            {
                case BeatmapBackground beatmapBackground:
                    return getBeatmapBackgroundStream(beatmapBackground.Beatmap)
                           ?? getBundledTextureStream(beatmapBackground.TextureName);

                case SkinBackground skinBackground:
                    return getSkinBackgroundStream(skinBackground)
                           ?? getBundledTextureStream(skinBackground.TextureName);

                case SeasonalBackground:
                    // the remote image is not usable (either it failed to load, or it is no longer downloadable),
                    // so the bundled image showing in its place is what gets saved.
                    return getBundledTextureStream(SeasonalBackground.FALLBACK_TEXTURE_NAME);

                default:
                    return getBundledTextureStream(displayed?.TextureName);
            }
        }

        private Stream? getBeatmapBackgroundStream(WorkingBeatmap working)
        {
            string? backgroundFile = working.Metadata?.BackgroundFile;

            if (string.IsNullOrEmpty(backgroundFile))
                return null;

            string? path = working.BeatmapSetInfo.GetPathForFile(backgroundFile);

            return path == null ? null : working.GetStream(path);
        }

        /// <summary>
        /// Read the image the given skin background is displaying from the skin's files.
        /// </summary>
        private Stream? getSkinBackgroundStream(SkinBackground background)
        {
            var skin = background.DisplayedSkin;

            var skinStore = new RealmBackedResourceStore<SkinInfo>(skin.SkinInfo, new RealmFileStore(realm, storage).Store, realm);

            // mirror the lookup order used when the skin is queried for a texture: the requested name first,
            // then the legacy one, and the high resolution variant ahead of the regular one.
            string[] lookupNames = background.PrimaryLookupName == @"menu-background"
                ? [background.PrimaryLookupName]
                : [background.PrimaryLookupName, @"menu-background"];

            foreach (string lookupName in lookupNames)
            {
                foreach (string name in new[] { $"{lookupName}@2x", lookupName })
                {
                    foreach (string extension in (string[])[@".png", @".jpg", @".jpeg"])
                    {
                        var stream = skinStore.GetStream($"{name}{extension}");

                        if (stream != null)
                            return stream;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Read a bundled image from the game resources.
        /// </summary>
        /// <param name="textureName">A texture name as used by the texture store, which carries no file extension.</param>
        private Stream? getBundledTextureStream(string? textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return null;

            string path = $@"{bundled_texture_namespace}/{textureName}";

            // the resource store performs an exact lookup, so the extension the texture store would have probed
            // has to be supplied explicitly.
            return game.Resources.GetStream($"{path}.jpg") ?? game.Resources.GetStream($"{path}.png");
        }
    }
}

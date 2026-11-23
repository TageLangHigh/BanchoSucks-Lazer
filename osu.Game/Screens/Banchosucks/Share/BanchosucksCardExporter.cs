// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Screens.Banchosucks.Share
{
    /// <summary>
    /// Banchosucks: makes a picture out of a share card (score card, profile card) and hands it to the player:
    /// saved as PNG under <c>screenshots/cards</c> in the game's folder and copied to the clipboard.
    /// The picture never leaves the PC through the client; sharing it is up to the player.
    /// </summary>
    /// <remarks>
    /// The card is an ordinary drawable of <see cref="CARD_WIDTH"/> x <see cref="CARD_HEIGHT"/>. It is rendered off
    /// screen by <see cref="BanchosucksDrawableCapture"/>, so the picture has that size in pixels on every monitor.
    /// </remarks>
    public partial class BanchosucksCardExporter : CompositeDrawable
    {
        public const int CARD_WIDTH = 1200;
        public const int CARD_HEIGHT = 630;

        /// <summary>
        /// How long the renderer has to deliver the picture, in milliseconds, loading of the card included.
        /// </summary>
        internal double Timeout = 8000;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private Storage storage = null!;

        private BanchosucksDrawableCapture? capture;
        private ScheduledDelegate? timeout;
        private TaskCompletionSource<string?>? running;

        /// <summary>
        /// Whether a card is being made right now.
        /// </summary>
        public bool Busy => running != null;

        public BanchosucksCardExporter()
        {
            // a parent of full size is never masked away, which the capture relies on
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            this.storage = storage.GetStorageForDirectory(@"screenshots").GetStorageForDirectory(@"cards");
        }

        /// <summary>
        /// Renders a card, saves it and copies it to the clipboard. Must be called from the update thread.
        /// </summary>
        /// <param name="card">The card, <see cref="CARD_WIDTH"/> x <see cref="CARD_HEIGHT"/>, not loaded yet.</param>
        /// <param name="name">What the file is called (a time stamp and ".png" are added).</param>
        /// <param name="what">The kind of card for the notification, in English and German, e.g. ("Score card", "Score-Karte").</param>
        /// <returns>The file name inside <c>screenshots/cards</c>, or null when no picture was made.</returns>
        public Task<string?> Export(Drawable card, string name, (string English, string German) what)
        {
            if (running != null)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = ClickerStrings.Text("A card is being created already, one moment.", "Es wird gerade schon eine Karte erstellt, einen Moment."),
                });
                return Task.FromResult<string?>(null);
            }

            var completion = running = new TaskCompletionSource<string?>();

            var content = new Container
            {
                Size = new Vector2(CARD_WIDTH, CARD_HEIGHT + BanchosucksCardImage.MARKER_ROWS),
                Children = new[]
                {
                    card,
                    // tells the top of the picture from the bottom after reading it back, see BanchosucksCardImage
                    new Box
                    {
                        Y = CARD_HEIGHT,
                        Size = new Vector2(CARD_WIDTH, BanchosucksCardImage.MARKER_ROWS),
                        Colour = new Color4(BanchosucksCardImage.MARKER_COLOUR.R, BanchosucksCardImage.MARKER_COLOUR.G, BanchosucksCardImage.MARKER_COLOUR.B, (byte)255),
                    },
                }
            };

            var newCapture = capture = new BanchosucksDrawableCapture(content, CARD_WIDTH, CARD_HEIGHT + BanchosucksCardImage.MARKER_ROWS, image => captured(completion, image, name, what));

            // also covers a card that fails to load: nothing would ever call back then
            timeout = Scheduler.AddDelayed(() =>
            {
                Logger.Log($"Banchosucks: no card image after {Timeout:N0} ms ({newCapture.DrawnFrames} frames drawn, load state {newCapture.LoadState})", level: LogLevel.Error);
                fail(completion, what);
            }, Timeout);

            LoadComponentAsync(newCapture, loaded =>
            {
                if (running == completion)
                    AddInternal(loaded);
                else
                    // given up in the meantime (the card took longer to load than the timeout allows)
                    loaded.Dispose();
            });

            return completion.Task;
        }

        private void captured(TaskCompletionSource<string?> completion, Image<Rgba32>? raw, string name, (string English, string German) what)
        {
            if (running != completion)
            {
                raw?.Dispose();
                return;
            }

            timeout?.Cancel();
            capture = null;

            if (raw == null)
            {
                fail(completion, what);
                return;
            }

            // cropping, encoding and writing take a moment for a picture of this size
            Task.Run(() =>
            {
                try
                {
                    using (raw)
                    using (var image = BanchosucksCardImage.Finish(raw, CARD_WIDTH, CARD_HEIGHT))
                    {
                        if (image == null || !BanchosucksCardImage.HasContent(image))
                        {
                            Logger.Log($"Banchosucks: the card image read back is not usable ({raw.Width}x{raw.Height})", level: LogLevel.Error);
                            Schedule(() => fail(completion, what));
                            return;
                        }

                        string filename = BanchosucksCardImage.TimestampedFileName(name, DateTime.Now);

                        using (var stream = storage.GetStream(filename, FileAccess.Write, FileMode.Create))
                            image.SaveAsPng(stream);

                        bool copied = clipboard.SetImage(image);

                        Schedule(() => succeed(completion, filename, what, copied));
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Banchosucks: saving the card image failed");
                    Schedule(() => fail(completion, what));
                }
            });
        }

        private void succeed(TaskCompletionSource<string?> completion, string filename, (string English, string German) what, bool copied)
        {
            if (running == completion)
                running = null;

            notifications?.Post(new SimpleNotification
            {
                // the kind of card is fixed text of ours, so it can go straight into the templates
                Text = copied
                    ? ClickerStrings.Text($"{what.English} saved and copied to the clipboard: {{0}}. Click to show the file.",
                        $"{what.German} gespeichert und in die Zwischenablage kopiert: {{0}}. Klicken zeigt die Datei.", filename)
                    : ClickerStrings.Text($"{what.English} saved: {{0}}. Click to show the file.", $"{what.German} gespeichert: {{0}}. Klicken zeigt die Datei.", filename),
                Icon = FontAwesome.Solid.Image,
                Activated = () =>
                {
                    storage.PresentFileExternally(filename);
                    return true;
                },
            });

            completion.TrySetResult(filename);
        }

        private void fail(TaskCompletionSource<string?> completion, (string English, string German) what)
        {
            if (running != completion)
                return;

            running = null;
            timeout?.Cancel();
            capture?.Abandon();
            capture = null;

            notifications?.Post(new SimpleNotification
            {
                Text = ClickerStrings.Text($"{what.English} could not be created, details are in the log.", $"{what.German} konnte nicht erstellt werden, Details stehen im Log."),
                Icon = FontAwesome.Solid.ExclamationTriangle,
            });

            completion.TrySetResult(null);
        }
    }
}

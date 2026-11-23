// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Screens.Banchosucks.Share
{
    /// <summary>
    /// Banchosucks: turns the pixels read back from the renderer into the finished card image, and names the file.
    /// </summary>
    /// <remarks>
    /// The card is rendered with a thin strip in a signal colour below it. Renderers do not agree on whether the
    /// first row read back from a frame buffer is the top or the bottom one, so the strip tells: wherever it ends
    /// up, that is the bottom. An image without the strip was not rendered properly and is refused, so a blank or
    /// garbled picture never reaches the player's disk.
    /// </remarks>
    public static class BanchosucksCardImage
    {
        /// <summary>
        /// Height of the strip below the card, in pixels.
        /// </summary>
        public const int MARKER_ROWS = 4;

        /// <summary>
        /// The colour of the strip. Nothing on a card fills whole rows with it.
        /// </summary>
        public static readonly Rgba32 MARKER_COLOUR = new Rgba32(255, 0, 255, 255);

        /// <summary>
        /// Checks, straightens and crops a rendered card.
        /// </summary>
        /// <param name="raw">What was read back: the card plus the marker strip, <paramref name="width"/> x (<paramref name="height"/> + <see cref="MARKER_ROWS"/>).</param>
        /// <param name="width">Width of the card in pixels.</param>
        /// <param name="height">Height of the card in pixels.</param>
        /// <returns>The card, upright, fully opaque and without the strip, or null when <paramref name="raw"/> is not a rendered card.</returns>
        public static Image<Rgba32>? Finish(Image<Rgba32> raw, int width, int height)
        {
            if (raw.Width != width || raw.Height != height + MARKER_ROWS)
                return null;

            // only the outer half of the strip is checked: the row next to the card may be blended with it
            const int checked_rows = MARKER_ROWS / 2;

            bool markerAtBottom = Enumerable.Range(raw.Height - checked_rows, checked_rows).All(y => isMarkerRow(raw, y));
            bool markerAtTop = Enumerable.Range(0, checked_rows).All(y => isMarkerRow(raw, y));

            // neither: nothing was rendered. both: a picture made of the marker colour, which is not a card either.
            if (markerAtBottom == markerAtTop)
                return null;

            var card = raw.Clone(context =>
            {
                if (markerAtTop)
                    context.Flip(FlipMode.Vertical);

                context.Crop(new Rectangle(0, 0, width, height));
            });

            // the card covers everything, so nothing may stay see-through (blending into an empty frame buffer can leave alpha below 255)
            card.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    foreach (ref var pixel in accessor.GetRowSpan(y))
                        pixel.A = 255;
                }
            });

            return card;
        }

        /// <summary>
        /// Whether a card image has any content: at least two clearly different colours.
        /// </summary>
        public static bool HasContent(Image<Rgba32> image)
        {
            Rgba32 first = image[0, 0];

            for (int y = 0; y < image.Height; y += Math.Max(1, image.Height / 32))
            {
                for (int x = 0; x < image.Width; x += Math.Max(1, image.Width / 64))
                {
                    Rgba32 pixel = image[x, y];

                    if (Math.Abs(pixel.R - first.R) + Math.Abs(pixel.G - first.G) + Math.Abs(pixel.B - first.B) > 24)
                        return true;
                }
            }

            return false;
        }

        private static bool isMarkerRow(Image<Rgba32> image, int y)
        {
            // a handful of pixels across the row; filtering and blending may shift the values a little
            for (int i = 0; i < 16; i++)
            {
                int x = (int)((i + 0.5f) / 16 * image.Width);
                Rgba32 pixel = image[x, y];

                if (pixel.R < 200 || pixel.G > 60 || pixel.B < 200)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// A file name (without extension) from free text: no characters Windows, Linux or macOS refuse, no dots
        /// or spaces at the ends, at most 80 characters, never empty.
        /// </summary>
        public static string SafeFileName(string text)
        {
            const string forbidden = "<>:\"/\\|?*";

            var cleaned = new string(text.Select(c => c < 32 || forbidden.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');

            if (cleaned.Length > 80)
                cleaned = cleaned[..80].Trim(' ', '.');

            return cleaned.Length == 0 ? "card" : cleaned;
        }

        /// <summary>
        /// "name_2026-11-23_18-04-05.png".
        /// </summary>
        public static string TimestampedFileName(string name, DateTime time) => $"{SafeFileName(name)}_{time:yyyy-MM-dd_HH-mm-ss}.png";
    }
}

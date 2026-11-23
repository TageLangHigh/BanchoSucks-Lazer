// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Screens.Banchosucks.Share;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: what happens to the pixels read back from the renderer before a share card is saved.
    /// </summary>
    [TestFixture]
    public class BanchosucksCardImageTest
    {
        private const int width = 120;
        private const int height = 63;

        private static readonly Rgba32 top_colour = new Rgba32(10, 20, 30, 255);
        private static readonly Rgba32 bottom_colour = new Rgba32(200, 180, 40, 255);

        /// <summary>
        /// A card as the renderer delivers it: top half one colour, bottom half another, the marker strip below.
        /// </summary>
        private static Image<Rgba32> createRendered(byte alpha = 255)
        {
            var image = new Image<Rgba32>(width, height + BanchosucksCardImage.MARKER_ROWS);

            for (int y = 0; y < image.Height; y++)
            {
                Rgba32 colour = y >= height ? BanchosucksCardImage.MARKER_COLOUR : y < height / 2 ? top_colour : bottom_colour;

                if (y < height)
                    colour.A = alpha;

                for (int x = 0; x < image.Width; x++)
                    image[x, y] = colour;
            }

            return image;
        }

        [Test]
        public void TestUprightImageIsCropped()
        {
            using var raw = createRendered();
            using var card = BanchosucksCardImage.Finish(raw, width, height);

            Assert.That(card, Is.Not.Null);
            Assert.That(card!.Width, Is.EqualTo(width));
            Assert.That(card.Height, Is.EqualTo(height));
            Assert.That(card[5, 0], Is.EqualTo(top_colour));
            Assert.That(card[5, height - 1], Is.EqualTo(bottom_colour), "the marker strip is gone, the last row is the card's");
        }

        [Test]
        public void TestUpsideDownImageIsTurned()
        {
            using var raw = createRendered();
            raw.Mutate(c => c.Flip(FlipMode.Vertical));

            // this is what a renderer that reads bottom row first hands over: marker on top
            Assert.That(raw[5, 0], Is.EqualTo(BanchosucksCardImage.MARKER_COLOUR));

            using var card = BanchosucksCardImage.Finish(raw, width, height);

            Assert.That(card, Is.Not.Null);
            Assert.That(card!.Height, Is.EqualTo(height));
            Assert.That(card[5, 0], Is.EqualTo(top_colour));
            Assert.That(card[5, height - 1], Is.EqualTo(bottom_colour));
        }

        [Test]
        public void TestResultIsOpaque()
        {
            using var raw = createRendered(alpha: 120);
            using var card = BanchosucksCardImage.Finish(raw, width, height);

            Assert.That(card, Is.Not.Null);
            Assert.That(card![0, 0].A, Is.EqualTo(255));
            Assert.That(card[width - 1, height - 1].A, Is.EqualTo(255));
            Assert.That(card[0, 0].R, Is.EqualTo(top_colour.R), "colours are kept");
        }

        [Test]
        public void TestBlankImageIsRefused()
        {
            // what an empty frame buffer looks like: nothing was rendered
            using var blank = new Image<Rgba32>(width, height + BanchosucksCardImage.MARKER_ROWS);

            Assert.That(BanchosucksCardImage.Finish(blank, width, height), Is.Null);
        }

        [Test]
        public void TestWrongSizeIsRefused()
        {
            using var raw = createRendered();
            using var smaller = raw.Clone(c => c.Crop(new Rectangle(0, 0, width - 1, raw.Height)));

            Assert.That(BanchosucksCardImage.Finish(smaller, width, height), Is.Null);

            using var withoutStrip = raw.Clone(c => c.Crop(new Rectangle(0, 0, width, height)));

            Assert.That(BanchosucksCardImage.Finish(withoutStrip, width, height), Is.Null);
        }

        [Test]
        public void TestImageFullOfMarkerColourIsRefused()
        {
            using var image = new Image<Rgba32>(width, height + BanchosucksCardImage.MARKER_ROWS, BanchosucksCardImage.MARKER_COLOUR);

            Assert.That(BanchosucksCardImage.Finish(image, width, height), Is.Null);
        }

        [Test]
        public void TestSlightlyOffMarkerStillCounts()
        {
            using var raw = createRendered();

            // filtering can shift the strip's colour a little, and the row next to the card can be mixed with it
            for (int x = 0; x < width; x++)
            {
                raw[x, height] = new Rgba32(120, 90, 130, 255);
                raw[x, height + 3] = new Rgba32(250, 6, 247, 255);
            }

            using var card = BanchosucksCardImage.Finish(raw, width, height);

            Assert.That(card, Is.Not.Null);
            Assert.That(card![5, height - 1], Is.EqualTo(bottom_colour));
        }

        [Test]
        public void TestHasContent()
        {
            using var plain = new Image<Rgba32>(width, height, new Rgba32(18, 12, 20, 255));
            using var raw = createRendered();
            using var card = BanchosucksCardImage.Finish(raw, width, height);

            Assert.That(BanchosucksCardImage.HasContent(plain), Is.False);
            Assert.That(BanchosucksCardImage.HasContent(card!), Is.True);
        }

        [TestCase("peppy - Some Song [Insane]", "peppy - Some Song [Insane]")]
        [TestCase("AC/DC: T.N.T? <live> \"x\" | a\\b * c", "AC_DC_ T.N.T_ _live_ _x_ _ a_b _ c")]
        [TestCase("  dots and spaces ...  ", "dots and spaces")]
        [TestCase("", "card")]
        [TestCase("...", "card")]
        [TestCase("tab\there", "tab_here")]
        [TestCase("日本語のタイトル", "日本語のタイトル")]
        public void TestSafeFileName(string text, string expected)
        {
            Assert.That(BanchosucksCardImage.SafeFileName(text), Is.EqualTo(expected));
        }

        [Test]
        public void TestSafeFileNameIsLimited()
        {
            string name = BanchosucksCardImage.SafeFileName(new string('a', 79) + " " + new string('b', 50));

            Assert.That(name.Length, Is.LessThanOrEqualTo(80));
            Assert.That(name, Does.Not.EndWith(" "));
        }

        [Test]
        public void TestTimestampedFileName()
        {
            string name = BanchosucksCardImage.TimestampedFileName("peppy: map", new DateTime(2026, 11, 23, 18, 4, 5));

            Assert.That(name, Is.EqualTo("peppy_ map_2026-11-23_18-04-05.png"));
        }
    }
}

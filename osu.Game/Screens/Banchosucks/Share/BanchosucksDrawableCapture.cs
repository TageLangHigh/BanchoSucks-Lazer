// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Screens.Banchosucks.Share
{
    /// <summary>
    /// Banchosucks: renders a drawable into a frame buffer of a fixed pixel size, off screen, and reads the pixels back.
    /// </summary>
    /// <remarks>
    /// This is the framework's own way of taking a picture of a drawable (its draw visualiser does the same with an
    /// internal class): a buffered draw node renders the children into a frame buffer, and
    /// <see cref="IRenderer.ExtractFrameBufferData"/> reads that buffer back on the draw thread. Nothing is drawn to
    /// the window, so the picture does not depend on the window size, UI scale or what is on screen.
    /// The content is laid out in pixels: this drawable pretends to sit at the top-left of the screen without any
    /// scaling, which makes the frame buffer exactly <c>width</c> x <c>height</c>.
    /// It must be added to a parent that is drawn (it is never masked away by it), and it removes itself when done.
    /// </remarks>
    public partial class BanchosucksDrawableCapture : CompositeDrawable, IBufferedDrawable
    {
        /// <summary>
        /// The picture is taken once this many frames were drawn and <see cref="capture_after_ms"/> have passed.
        /// The first frames are left to texture uploads (background picture, avatar, glyphs), which the renderer
        /// spreads over frames; the time covers very high frame rates, where six frames are over in a few milliseconds.
        /// </summary>
        private const int capture_after_frames = 6;

        private const long capture_after_ms = 120;

        private readonly int width;
        private readonly int height;

        private readonly BufferedDrawNodeSharedData sharedData = new BufferedDrawNodeSharedData(TexturePixelFormat.R8G8B8A8Float, new[] { RenderBufferFormat.D16 }, pixelSnapping: true, clipToRootNode: false);

        private IShader textureShader = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private Action<Image<Rgba32>?>? onCaptured;

        // touched by the draw thread
        private int drawnFrames;
        private int captureStarted;
        private long firstDrawn;

        /// <summary>
        /// How many frames of this were drawn so far (0 on a host that does not draw, like the headless one of the tests).
        /// </summary>
        public int DrawnFrames => Volatile.Read(ref drawnFrames);

        /// <param name="content">What to take a picture of. Laid out in a <paramref name="width"/> x <paramref name="height"/> area, one unit per pixel.</param>
        /// <param name="width">Width of the picture in pixels.</param>
        /// <param name="height">Height of the picture in pixels.</param>
        /// <param name="onCaptured">Called once on the update thread with the pixels, or with null when reading them back failed.</param>
        public BanchosucksDrawableCapture(Drawable content, int width, int height, Action<Image<Rgba32>?> onCaptured)
        {
            this.width = width;
            this.height = height;
            this.onCaptured = onCaptured;

            Size = new Vector2(width, height);
            InternalChild = content;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            textureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        #region Pretend to be a width x height rectangle at the top-left of the screen, unscaled and fully visible

        public override Quad ScreenSpaceDrawQuad => new Quad(0, 0, width, height);

        public override DrawInfo DrawInfo => new DrawInfo(null);

        public override DrawColourInfo DrawColourInfo => new DrawColourInfo(Color4.White);

        public override bool IsPresent => true;

        // the pretended place on screen must not catch the mouse or the keyboard for the moment this exists
        public override bool PropagatePositionalInputSubTree => false;

        public override bool PropagateNonPositionalInputSubTree => false;

        // neither this nor its children are ever masked away, also when the window (or the scaled UI area) is smaller than the picture
        protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;

        protected override RectangleF ComputeChildMaskingBounds() => ScreenSpaceDrawQuad.AABBFloat;

        #endregion

        #region IBufferedDrawable

        public IShader TextureShader => textureShader;

        public Color4 BackgroundColour => new Color4(0, 0, 0, 0);

        public DrawColourInfo? FrameBufferDrawColour => new DrawColourInfo(Color4.White);

        public Vector2 FrameBufferScale => Vector2.One;

        #endregion

        protected override void Update()
        {
            base.Update();

            // render again every frame until the picture is taken
            Invalidate(Invalidation.DrawNode);
        }

        protected override DrawNode CreateDrawNode() => new CaptureDrawNode(this, new CompositeDrawableDrawNode(this), sharedData);

        /// <summary>
        /// Called on the draw thread after the content was rendered into the frame buffer.
        /// </summary>
        private void rendered(IFrameBuffer frameBuffer)
        {
            long now = Environment.TickCount64;

            if (Interlocked.Increment(ref drawnFrames) == 1)
                firstDrawn = now;

            if (Volatile.Read(ref drawnFrames) < capture_after_frames || now - firstDrawn < capture_after_ms)
                return;

            if (Interlocked.Exchange(ref captureStarted, 1) == 1)
                return;

            // read back between two frames, as the framework does it
            host.DrawThread.Scheduler.Add(() =>
            {
                Image<Rgba32>? image = null;

                try
                {
                    image = renderer.ExtractFrameBufferData(frameBuffer);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Banchosucks: reading back the card image failed");
                }

                Schedule(() => finish(image));
            });
        }

        private void finish(Image<Rgba32>? image)
        {
            var callback = onCaptured;
            onCaptured = null;

            if (callback != null)
                callback(image);
            else
                image?.Dispose();

            Expire();
        }

        /// <summary>
        /// Gives up (nothing was drawn in time). The callback is not called any more.
        /// </summary>
        public void Abandon()
        {
            onCaptured = null;
            Interlocked.Exchange(ref captureStarted, 1);
            Expire();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            sharedData.Dispose();
        }

        private class CaptureDrawNode : BufferedDrawNode, ICompositeDrawNode
        {
            protected new BanchosucksDrawableCapture Source => (BanchosucksDrawableCapture)base.Source;

            protected new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            public CaptureDrawNode(BanchosucksDrawableCapture source, CompositeDrawableDrawNode child, BufferedDrawNodeSharedData sharedData)
                : base(source, child, sharedData)
            {
            }

            /// <summary>
            /// The content is in the frame buffer now. It is not drawn to the window; the pixels are read back instead.
            /// </summary>
            protected override void DrawContents(IRenderer renderer) => Source.rendered(SharedData.MainBuffer);

            public List<DrawNode>? Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            public bool AddChildDrawNodes => RequiresRedraw;
        }
    }
}

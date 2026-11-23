// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A form control offering a fixed set of options, of which exactly one can be selected at a time.
    /// </summary>
    /// <remarks>
    /// The options are laid out along a single axis and are all given the same size, which makes this suitable
    /// for a small set of mutually exclusive choices. Each option may be presented as an icon, a label, or both.
    /// </remarks>
    public partial class FormToggleGroup<T> : CompositeDrawable, IHasCurrentValue<T>, IFormControl
    {
        private const float content_spacing = 5;

        /// <summary>
        /// The direction the options are laid out in.
        /// </summary>
        /// <remarks>
        /// Only <see cref="FillDirection.Horizontal"/> and <see cref="FillDirection.Vertical"/> are supported.
        /// </remarks>
        public FillDirection Direction { get; init; } = FillDirection.Horizontal;

        /// <summary>
        /// The options offered by this control, in display order.
        /// </summary>
        public IEnumerable<FormToggleGroupItem<T>> Items { get; init; } = Array.Empty<FormToggleGroupItem<T>>();

        /// <summary>
        /// Caption describing this control, displayed on top of the options.
        /// </summary>
        public LocalisableString Caption { get; init; }

        /// <summary>
        /// Hint text containing an extended description of this control, displayed in a tooltip when hovering the caption.
        /// </summary>
        public LocalisableString HintText { get; init; }

        private readonly BindableWithCurrent<T> current = new BindableWithCurrent<T>();

        public Bindable<T> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        private FormToggleGroupItem<T>[] optionItems = Array.Empty<FormToggleGroupItem<T>>();

        private FormControlBackground background = null!;
        private FormFieldCaption? caption;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public FormToggleGroup()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            optionItems = Items.ToArray();

            var content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, content_spacing),
            };

            if (Caption != default)
                content.Add(caption = new FormFieldCaption { Caption = Caption, TooltipText = HintText });

            if (optionItems.Length > 0)
                content.Add(createOptions(optionItems));

            foreach (var item in optionItems)
                item.Action = () => Current.Value = item.Value;

            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(9),
                    Child = content,
                },
            };
        }

        /// <summary>
        /// Lays the options out over the full width of the control, each of them taking an equal share of it.
        /// </summary>
        private GridContainer createOptions(FormToggleGroupItem<T>[] items)
        {
            bool vertical = Direction == FillDirection.Vertical;

            var grid = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            if (vertical)
            {
                // one option per row, all of them equally as tall as one another.
                grid.ColumnDimensions = [new Dimension()];
                grid.RowDimensions = items.Select(_ => new Dimension(GridSizeMode.AutoSize)).ToArray();
                grid.Content = items.Select(item => new Drawable[] { item }).ToArray();
            }
            else
            {
                // all options on a single row, the width being split between them.
                grid.RowDimensions = [new Dimension(GridSizeMode.AutoSize)];
                grid.ColumnDimensions = items.Select(_ => new Dimension()).ToArray();
                grid.Content = new[] { items.Cast<Drawable>().ToArray() };
            }

            return grid;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            colourProvider.ColoursChanged += updateState;
            Current.BindValueChanged(_ =>
            {
                updateSelection();
                ValueChanged?.Invoke();
            }, true);
            Current.BindDisabledChanged(_ => updateState(), true);
        }

        protected override void Dispose(bool isDisposing)
        {
            // this drawable can be disposed before it has ever been loaded (eg. the game being torn down
            // while the overlay hosting it is still initialising), in which case the provider is still null.
            if (isDisposing && LoadState >= LoadState.Ready)
                colourProvider.ColoursChanged -= updateState;

            base.Dispose(isDisposing);
        }

        private void updateSelection()
        {
            foreach (var item in optionItems)
                item.Selected = EqualityComparer<T>.Default.Equals(Current.Value, item.Value);
        }

        private void updateState()
        {
            background.VisualStyle = Current.Disabled ? VisualStyle.Disabled : VisualStyle.Normal;

            caption?.Colour = Current.Disabled ? colourProvider.Background1 : colourProvider.Content2;

            foreach (var item in optionItems)
                item.Enabled.Value = !Current.Disabled;
        }

        public IEnumerable<LocalisableString> FilterTerms => Caption.Yield().Concat(optionItems.Select(item => item.Text));

        public event Action? ValueChanged;

        public bool IsDefault => Current.IsDefault;

        public void SetDefault() => Current.SetDefault();

        public bool IsDisabled => Current.Disabled;

        public float MainDrawHeight => DrawHeight;
    }
}

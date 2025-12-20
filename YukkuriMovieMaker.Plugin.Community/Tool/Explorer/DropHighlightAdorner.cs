using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Documents;


namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    using System.Windows.Media;

    public sealed class DropHighlightAdorner : Adorner
    {
        readonly Pen pen;
        readonly Brush? fill;
        readonly Thickness inset;

        public DropHighlightAdorner(
            FrameworkElement adornedElement)
            : base(adornedElement)
        {
            IsHitTestVisible = false;

            this.inset = new Thickness(1);
            var borderBrush = adornedElement.TryFindResource(SystemColors.HighlightBrushKey) as Brush ?? Brushes.Red;
            fill = adornedElement.TryFindResource(SystemColors.HighlightBrushKey) as Brush ?? Brushes.Transparent;

            pen = new Pen(borderBrush, 1);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var size = AdornedElement.RenderSize;

            var rect = new Rect(
                inset.Left,
                inset.Top,
                Math.Max(0, size.Width - inset.Left - inset.Right),
                Math.Max(0, size.Height - inset.Top - inset.Bottom));

            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            dc.DrawRectangle(null, pen, rect);
        }
    }
}
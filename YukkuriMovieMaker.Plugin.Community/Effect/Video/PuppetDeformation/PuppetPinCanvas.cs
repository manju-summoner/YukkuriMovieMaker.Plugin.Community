using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    /// <summary>
    /// アイテムの画像を表示し、基準ピンの追加・移動・削除を行うキャンバス。
    /// クリックでピン追加、ドラッグで基準位置の移動、右クリックまたはDeleteキーで削除。
    /// 移動ピン（オフセット）の編集はメインプレビュー側で行う。
    /// </summary>
    internal sealed class PuppetPinCanvas : FrameworkElement
    {
        const double PinRadius = 5.0;
        const double PinHitRadius = 9.0;

        static readonly System.Windows.Media.Brush CheckerBrush = CreateCheckerBrush();
        static readonly System.Windows.Media.Brush PinFillBrush = CreateFrozenBrush(Color.FromRgb(0x2E, 0x86, 0xFF));
        static readonly System.Windows.Media.Brush DisabledPinFillBrush = CreateFrozenBrush(Color.FromArgb(0xA0, 0x80, 0x80, 0x80));
        static readonly Pen PinStrokePen = CreateFrozenPen(Colors.White, 1.5);
        static readonly Pen PinHaloPen = CreateFrozenPen(Color.FromArgb(0x80, 0x00, 0x00, 0x00), 3.5);
        static readonly Pen SelectionPen = CreateFrozenPen(Color.FromRgb(0xFF, 0xC8, 0x00), 2.0);
        static readonly System.Windows.Media.Brush LabelBrush = CreateFrozenBrush(Colors.White);

        PuppetDeformationListEditorViewModel? viewModel;
        ImmutableList<PuppetDeformationItemViewModel> pins = [];

        bool isDragging;
        bool dragMoved;
        Point lastDragPosition;

        public PuppetPinCanvas()
        {
            Focusable = true;
            ClipToBounds = true;
            DataContextChanged += OnDataContextChanged;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (viewModel is not null)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                DetachPins();
            }
            viewModel = e.NewValue as PuppetDeformationListEditorViewModel;
            if (viewModel is not null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
                AttachPins();
            }
            InvalidateVisual();
        }

        void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.CanvasImage))
            {
                InvalidateVisual();
            }
            else if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.CanvasPins))
            {
                DetachPins();
                AttachPins();
                InvalidateVisual();
            }
        }

        void AttachPins()
        {
            pins = viewModel?.CanvasPins ?? [];
            foreach (var pin in pins)
            {
                pin.PropertyChanged += Pin_PropertyChanged;
                pin.RestChanged += Pin_RestChanged;
            }
        }

        void DetachPins()
        {
            foreach (var pin in pins)
            {
                pin.PropertyChanged -= Pin_PropertyChanged;
                pin.RestChanged -= Pin_RestChanged;
            }
            pins = [];
        }

        void Pin_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(PuppetDeformationItemViewModel.IsRestSelected)
                or nameof(PuppetDeformationItemViewModel.IsOffsetSelected)
                or nameof(PuppetDeformationItemViewModel.IsEnabled))
            {
                InvalidateVisual();
            }
        }

        void Pin_RestChanged(object? sender, EventArgs e) => InvalidateVisual();

        #region 座標変換

        (double Scale, Point Origin, double ImageWidth, double ImageHeight)? GetLayout()
        {
            var image = viewModel?.CanvasImage;
            if (image is null || RenderSize.Width <= 0 || RenderSize.Height <= 0)
                return null;

            double iw = image.PixelWidth;
            double ih = image.PixelHeight;
            if (iw <= 0 || ih <= 0)
                return null;

            var scale = Math.Min(RenderSize.Width / iw, RenderSize.Height / ih);
            var origin = new Point((RenderSize.Width - iw * scale) * 0.5, (RenderSize.Height - ih * scale) * 0.5);
            return (scale, origin, iw, ih);
        }

        static Point DisplayToLocal(Point display, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout)
            => new(
                (display.X - layout.Origin.X) / layout.Scale - layout.ImageWidth * 0.5,
                (display.Y - layout.Origin.Y) / layout.Scale - layout.ImageHeight * 0.5);

        static Point LocalToDisplay(Point local, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout)
            => new(
                layout.Origin.X + (local.X + layout.ImageWidth * 0.5) * layout.Scale,
                layout.Origin.Y + (local.Y + layout.ImageHeight * 0.5) * layout.Scale);

        static Point GetRestPoint(PuppetDeformationItemViewModel pin)
            => new(
                pin.Model.RestX.Values.FirstOrDefault()?.Value ?? 0,
                pin.Model.RestY.Values.FirstOrDefault()?.Value ?? 0);

        #endregion

        protected override void OnRender(DrawingContext drawingContext)
        {
            var bounds = new Rect(RenderSize);
            drawingContext.DrawRectangle(CheckerBrush, null, bounds);

            var layout = GetLayout();
            if (layout is null)
            {
                DrawCenteredText(drawingContext, Texts.PuppetDeformationCanvasNoImage, bounds);
                return;
            }
            var l = layout.Value;

            var image = viewModel!.CanvasImage!;
            drawingContext.DrawImage(image, new Rect(l.Origin, new Size(l.ImageWidth * l.Scale, l.ImageHeight * l.Scale)));

            foreach (var pin in pins)
            {
                var p = LocalToDisplay(GetRestPoint(pin), l);

                if (pin.IsRestSelected)
                    drawingContext.DrawEllipse(null, SelectionPen, p, PinRadius + 3.5, PinRadius + 3.5);

                drawingContext.DrawEllipse(null, PinHaloPen, p, PinRadius, PinRadius);
                drawingContext.DrawEllipse(pin.IsEnabled ? PinFillBrush : DisabledPinFillBrush, PinStrokePen, p, PinRadius, PinRadius);
            }
        }

        void DrawCenteredText(DrawingContext drawingContext, string text, Rect bounds)
        {
            var formatted = CreateFormattedText(text, LabelBrush);
            formatted.MaxTextWidth = Math.Max(1, bounds.Width - 8);
            drawingContext.DrawText(
                formatted,
                new Point(
                    bounds.Left + (bounds.Width - Math.Min(formatted.Width, formatted.MaxTextWidth)) * 0.5,
                    bounds.Top + (bounds.Height - formatted.Height) * 0.5));
        }

        FormattedText CreateFormattedText(string text, System.Windows.Media.Brush brush)
            => new(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

        #region マウス・キーボード操作

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            if (viewModel is null)
                return;
            var layout = GetLayout();
            if (layout is null)
                return;

            var pos = e.GetPosition(this);
            var hit = HitTestPin(pos, layout.Value);
            if (hit is not null)
            {
                viewModel.SelectRestFromCanvas(hit);
            }
            else
            {
                if (!viewModel.CanAddPin)
                    return;
                var local = DisplayToLocal(pos, layout.Value);
                viewModel.AddPinFromCanvas(local.X, local.Y);

                //追加でピン一覧が再構築されるため、追加されたピンを取り直してそのままドラッグできるようにする
                hit = HitTestPin(pos, layout.Value);
                if (hit is null)
                    return;
            }

            isDragging = true;
            dragMoved = false;
            lastDragPosition = pos;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!isDragging || viewModel is null)
                return;
            var layout = GetLayout();
            if (layout is null || layout.Value.Scale <= 0)
                return;

            var pos = e.GetPosition(this);
            var dx = (pos.X - lastDragPosition.X) / layout.Value.Scale;
            var dy = (pos.Y - lastDragPosition.Y) / layout.Value.Scale;
            if (dx == 0 && dy == 0)
                return;

            if (!dragMoved)
            {
                viewModel.BeginRestDragFromCanvas();
                dragMoved = true;
            }
            viewModel.MoveSelectedRestsFromCanvas(dx, dy);
            lastDragPosition = pos;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!isDragging)
                return;
            isDragging = false;
            ReleaseMouseCapture();
            if (dragMoved)
                viewModel?.EndRestDragFromCanvas();
            dragMoved = false;
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (!isDragging)
                return;
            isDragging = false;
            if (dragMoved)
                viewModel?.EndRestDragFromCanvas();
            dragMoved = false;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            if (TryRemovePinAt(e.GetPosition(this)))
                e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            //中クリックでも削除できるようにする（左右ボタンは専用ハンドラ側で処理する）
            if (e.ChangedButton != MouseButton.Middle)
                return;
            if (TryRemovePinAt(e.GetPosition(this)))
                e.Handled = true;
        }

        bool TryRemovePinAt(Point display)
        {
            if (viewModel is null)
                return false;
            var layout = GetLayout();
            if (layout is null)
                return false;

            var hit = HitTestPin(display, layout.Value);
            if (hit is null)
                return false;
            viewModel.RemovePinFromCanvas(hit);
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key != Key.Delete || viewModel is null)
                return;
            viewModel.RemoveSelectedRestPinsFromCanvas();
            e.Handled = true;
        }

        PuppetDeformationItemViewModel? HitTestPin(Point display, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout)
        {
            PuppetDeformationItemViewModel? nearest = null;
            var nearestDistSq = PinHitRadius * PinHitRadius;
            //後に描画される（手前の）ピンを優先する
            for (var i = pins.Count - 1; i >= 0; i--)
            {
                var p = LocalToDisplay(GetRestPoint(pins[i]), layout);
                var dx = p.X - display.X;
                var dy = p.Y - display.Y;
                var distSq = dx * dx + dy * dy;
                if (distSq <= nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = pins[i];
                }
            }
            return nearest;
        }

        #endregion

        static System.Windows.Media.Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        static Pen CreateFrozenPen(Color color, double thickness)
        {
            var pen = new Pen(CreateFrozenBrush(color), thickness);
            pen.Freeze();
            return pen;
        }

        static System.Windows.Media.Brush CreateCheckerBrush()
        {
            var dark = CreateFrozenBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            var light = CreateFrozenBrush(Color.FromRgb(0x33, 0x33, 0x33));
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
            group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
            var brush = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = System.Windows.Media.Stretch.None,
            };
            brush.Freeze();
            return brush;
        }
    }
}

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
    /// ピンモード: クリックでピン追加、ドラッグで基準位置の移動、右クリックまたはDeleteキーで削除。
    /// ボーンモード: クリックでボーン追加（選択中ボーンの子）、ボーン（親子リンクの線）をクリックでジョイント挿入（分割）、
    /// ピンをクリックで割当切替、ドラッグでジョイント移動。
    /// 移動ピン（オフセット）とボーンの回転はメインプレビュー側で行う。
    /// </summary>
    internal sealed class PuppetPinCanvas : FrameworkElement
    {
        const double PinRadius = 5.0;
        const double PinHitRadius = 9.0;
        const double BoneRadius = 6.5;
        const double BoneHitRadius = 10.0;
        //親子リンク（線分）へのクリック判定距離。ジョイント判定を優先するためやや小さめにする
        const double BoneSegmentHitRadius = 6.0;

        static readonly System.Windows.Media.Brush CheckerBrush = CreateCheckerBrush();
        static readonly System.Windows.Media.Brush PinFillBrush = CreateFrozenBrush(Color.FromRgb(0x2E, 0x86, 0xFF));
        static readonly System.Windows.Media.Brush DisabledPinFillBrush = CreateFrozenBrush(Color.FromArgb(0xA0, 0x80, 0x80, 0x80));
        static readonly Pen PinStrokePen = CreateFrozenPen(Colors.White, 1.5);
        static readonly Pen PinHaloPen = CreateFrozenPen(Color.FromArgb(0x80, 0x00, 0x00, 0x00), 3.5);
        static readonly System.Windows.Media.Brush LabelBrush = CreateFrozenBrush(Colors.White);
        static readonly System.Windows.Media.Brush BoneFillBrush = CreateFrozenBrush(Color.FromRgb(0xFF, 0x95, 0x00));
        static readonly System.Windows.Media.Brush DisabledBoneFillBrush = CreateFrozenBrush(Color.FromArgb(0xA0, 0x80, 0x80, 0x80));
        static readonly Pen BoneLinkPen = CreateFrozenPen(Color.FromArgb(0xC0, 0xFF, 0x95, 0x00), 2.0);
        //ジョイント挿入のためにホバー中の親子リンクを強調する線と、挿入位置に出す半透明のプレビュー
        static readonly Pen BoneSegmentHighlightPen = CreateFrozenPen(Color.FromRgb(0xFF, 0xC1, 0x66), 3.0);
        static readonly System.Windows.Media.Brush BoneInsertGhostBrush = CreateFrozenBrush(Color.FromArgb(0xB0, 0xFF, 0xC1, 0x66));
        //親子リンクの向き（親→子）を示す矢じりと、親を持たないルートジョイントを示す外周ひし形リング
        static readonly System.Windows.Media.Brush BoneDirectionBrush = CreateFrozenBrush(Color.FromRgb(0xFF, 0x95, 0x00));
        static readonly Pen BoneDirectionPen = CreateFrozenPen(Colors.White, 1.0);
        static readonly Pen BoneRootRingPen = CreateFrozenPen(Color.FromRgb(0xFF, 0xC1, 0x66), 1.5);
        //向き矢じりの大きさ(px)と、これより短いリンクには矢じりを描かない下限
        const double BoneArrowSize = 5.0;
        //ルートジョイントの外周リングを本体のひし形からどれだけ外に出すか(px)
        const double BoneRootRingMargin = 3.0;
        static readonly Pen AssignedPinPen = CreateFrozenPen(Color.FromRgb(0xFF, 0x95, 0x00), 2.0);
        //ボーンと割当ピンをつなぐ点線
        static readonly Pen BonePinLinkPen = CreateFrozenDashedPen(Color.FromArgb(0xB0, 0xFF, 0x95, 0x00), 1.5);
        //ピン編集モード時のボーンの表示透明度
        const double InactiveBoneOpacity = 0.4;
        //選択リングは各マーカーの塗りつぶしと同じ色にする
        static readonly Pen PinSelectionPen = CreateFrozenPen(Color.FromRgb(0x2E, 0x86, 0xFF), 2.0);
        static readonly Pen BoneSelectionPen = CreateFrozenPen(Color.FromRgb(0xFF, 0x95, 0x00), 2.0);
        //マウスオーバー時のマーカー拡大量(px)
        const double HoverRadiusBonus = 2.0;

        PuppetDeformationListEditorViewModel? viewModel;
        ImmutableList<PuppetDeformationItemViewModel> pins = [];
        ImmutableList<PuppetBoneViewModel> bones = [];

        bool isDragging;
        bool isBoneDragging;
        bool dragMoved;
        //選択済みジョイントをクリック（ドラッグなし）したときだけ選択解除するためのフラグ
        bool pressedOnSelectedBone;
        Point lastDragPosition;

        PuppetDeformationItemViewModel? hoveredPin;
        PuppetBoneViewModel? hoveredBone;
        //ジョイント挿入プレビュー：ホバー中の親子リンクの子ボーンと、線上に投影した挿入位置（表示座標）
        PuppetBoneViewModel? hoveredSegmentBone;
        Point hoveredSegmentPoint;

        //ズーム・パン（表示の拡大率と表示位置）。全体フィット表示を1.0とする拡大率と、
        //スクロール位置（コンテンツ左上からの表示px。ScrollOffsetX/Yと連動）で表示範囲を決める。
        const double MinZoom = 0.25;
        const double MaxZoom = 16.0;
        //ホイール1ノッチあたりの拡大率の倍率
        const double ZoomStep = 1.2;
        //中ドラッグでパンと判定するまでの移動しきい値（px）。これ未満なら中クリック=削除として扱う
        const double PanThreshold = 3.0;
        //表示の拡大率（フィット表示=1.0）。スクロール位置はScrollOffsetX/Yで持つ
        double zoom = 1.0;
        //画像サイズが変わったとき（別画像に差し替え時）に全体表示へ戻すための記録
        Size lastImageSize = Size.Empty;
        //レイアウト確定前にResetViewが呼ばれた場合、サイズ確定時に中央寄せを適用するための予約
        bool pendingResetView = true;

        bool isPanning;
        bool panMoved;
        Point lastPanPosition;

        /// <summary>横スクロールバーの現在位置（コンテンツ左端からの表示px）。ScrollBar.ValueとTwoWayで連動する。</summary>
        public double ScrollOffsetX
        {
            get => (double)GetValue(ScrollOffsetXProperty);
            set => SetValue(ScrollOffsetXProperty, value);
        }
        public static readonly DependencyProperty ScrollOffsetXProperty =
            DependencyProperty.Register(nameof(ScrollOffsetX), typeof(double), typeof(PuppetPinCanvas),
                new FrameworkPropertyMetadata(0.0, OnScrollOffsetChanged, CoerceScrollOffsetX));

        /// <summary>縦スクロールバーの現在位置（コンテンツ上端からの表示px）。ScrollBar.ValueとTwoWayで連動する。</summary>
        public double ScrollOffsetY
        {
            get => (double)GetValue(ScrollOffsetYProperty);
            set => SetValue(ScrollOffsetYProperty, value);
        }
        public static readonly DependencyProperty ScrollOffsetYProperty =
            DependencyProperty.Register(nameof(ScrollOffsetY), typeof(double), typeof(PuppetPinCanvas),
                new FrameworkPropertyMetadata(0.0, OnScrollOffsetChanged, CoerceScrollOffsetY));

        /// <summary>横スクロールの最大値（ScrollBar.Maximum）。</summary>
        public double ScrollMaxX
        {
            get => (double)GetValue(ScrollMaxXProperty);
            private set => SetValue(ScrollMaxXPropertyKey, value);
        }
        static readonly DependencyPropertyKey ScrollMaxXPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ScrollMaxX), typeof(double), typeof(PuppetPinCanvas),
                new FrameworkPropertyMetadata(0.0, OnScrollMaxXChanged));
        public static readonly DependencyProperty ScrollMaxXProperty = ScrollMaxXPropertyKey.DependencyProperty;

        /// <summary>縦スクロールの最大値（ScrollBar.Maximum）。</summary>
        public double ScrollMaxY
        {
            get => (double)GetValue(ScrollMaxYProperty);
            private set => SetValue(ScrollMaxYPropertyKey, value);
        }
        static readonly DependencyPropertyKey ScrollMaxYPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ScrollMaxY), typeof(double), typeof(PuppetPinCanvas),
                new FrameworkPropertyMetadata(0.0, OnScrollMaxYChanged));
        public static readonly DependencyProperty ScrollMaxYProperty = ScrollMaxYPropertyKey.DependencyProperty;

        /// <summary>横スクロールバーのつまみ比率に使うビューポート幅（ScrollBar.ViewportSize）。</summary>
        public double ScrollViewportW
        {
            get => (double)GetValue(ScrollViewportWProperty);
            private set => SetValue(ScrollViewportWPropertyKey, value);
        }
        static readonly DependencyPropertyKey ScrollViewportWPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ScrollViewportW), typeof(double), typeof(PuppetPinCanvas),
                new FrameworkPropertyMetadata(0.0));
        public static readonly DependencyProperty ScrollViewportWProperty = ScrollViewportWPropertyKey.DependencyProperty;

        /// <summary>縦スクロールバーのつまみ比率に使うビューポート高さ（ScrollBar.ViewportSize）。</summary>
        public double ScrollViewportH
        {
            get => (double)GetValue(ScrollViewportHProperty);
            private set => SetValue(ScrollViewportHPropertyKey, value);
        }
        static readonly DependencyPropertyKey ScrollViewportHPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ScrollViewportH), typeof(double), typeof(PuppetPinCanvas),
                new FrameworkPropertyMetadata(0.0));
        public static readonly DependencyProperty ScrollViewportHProperty = ScrollViewportHPropertyKey.DependencyProperty;

        /// <summary>横スクロールバーの表示/非表示（スクロールの余地があるときだけ表示）。</summary>
        public Visibility HScrollVisibility
        {
            get => (Visibility)GetValue(HScrollVisibilityProperty);
            private set => SetValue(HScrollVisibilityPropertyKey, value);
        }
        static readonly DependencyPropertyKey HScrollVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(HScrollVisibility), typeof(Visibility), typeof(PuppetPinCanvas),
                new FrameworkPropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty HScrollVisibilityProperty = HScrollVisibilityPropertyKey.DependencyProperty;

        /// <summary>縦スクロールバーの表示/非表示（スクロールの余地があるときだけ表示）。</summary>
        public Visibility VScrollVisibility
        {
            get => (Visibility)GetValue(VScrollVisibilityProperty);
            private set => SetValue(VScrollVisibilityPropertyKey, value);
        }
        static readonly DependencyPropertyKey VScrollVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(VScrollVisibility), typeof(Visibility), typeof(PuppetPinCanvas),
                new FrameworkPropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty VScrollVisibilityProperty = VScrollVisibilityPropertyKey.DependencyProperty;

        static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((PuppetPinCanvas)d).InvalidateVisual();

        static object CoerceScrollOffsetX(DependencyObject d, object baseValue)
            => Math.Clamp((double)baseValue, 0, ((PuppetPinCanvas)d).ScrollMaxX);

        static object CoerceScrollOffsetY(DependencyObject d, object baseValue)
            => Math.Clamp((double)baseValue, 0, ((PuppetPinCanvas)d).ScrollMaxY);

        static void OnScrollMaxXChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => d.CoerceValue(ScrollOffsetXProperty);

        static void OnScrollMaxYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => d.CoerceValue(ScrollOffsetYProperty);

        public PuppetPinCanvas()
        {
            Focusable = true;
            ClipToBounds = true;
            //マーカー操作の邪魔にならないよう、ツールチップはパネルの下側に出す
            System.Windows.Controls.ToolTipService.SetPlacement(this, System.Windows.Controls.Primitives.PlacementMode.Bottom);
            DataContextChanged += OnDataContextChanged;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (viewModel is not null)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                DetachPins();
                DetachBones();
            }
            viewModel = e.NewValue as PuppetDeformationListEditorViewModel;
            if (viewModel is not null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
                AttachPins();
                AttachBones();
            }
            UpdateToolTip();
            InvalidateVisual();
        }

        void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.CanvasImage))
            {
                //別サイズの画像に差し替わったときだけ全体表示へ戻す（同サイズの再取得ではズーム/パンを保つ）
                var image = viewModel?.CanvasImage;
                var size = image is null ? Size.Empty : new Size(image.PixelWidth, image.PixelHeight);
                if (size != lastImageSize)
                {
                    lastImageSize = size;
                    ResetView();
                }
                else
                {
                    UpdateScrollInfo();
                    InvalidateVisual();
                }
            }
            else if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.CanvasPins))
            {
                DetachPins();
                AttachPins();
                ClearHover();
                UpdateScrollInfo();
                InvalidateVisual();
            }
            else if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.CanvasBones))
            {
                DetachBones();
                AttachBones();
                ClearHover();
                UpdateScrollInfo();
                InvalidateVisual();
            }
            else if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.EditMode))
            {
                UpdateToolTip();
                ClearHover();
                InvalidateVisual();
            }
        }

        bool IsBoneMode => viewModel?.EditMode == PuppetDeformationEditMode.Bone;

        void UpdateToolTip()
        {
            ToolTip = IsBoneMode
                ? Texts.PuppetDeformationCanvasBoneTooltip
                : Texts.PuppetDeformationCanvasTooltip;
        }

        void AttachBones()
        {
            bones = viewModel?.CanvasBones ?? [];
            foreach (var bone in bones)
            {
                bone.PropertyChanged += Bone_PropertyChanged;
                bone.VisualChanged += Bone_VisualChanged;
            }
        }

        void DetachBones()
        {
            foreach (var bone in bones)
            {
                bone.PropertyChanged -= Bone_PropertyChanged;
                bone.VisualChanged -= Bone_VisualChanged;
            }
            bones = [];
        }

        void Bone_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(PuppetBoneViewModel.IsSelected) or nameof(PuppetBoneViewModel.IsEnabled))
                InvalidateVisual();
        }

        void Bone_VisualChanged(object? sender, EventArgs e)
        {
            //ジョイントが動くとコンテンツ範囲が変わるためスクロール量も更新する
            UpdateScrollInfo();
            InvalidateVisual();
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
                or nameof(PuppetDeformationItemViewModel.IsEnabled)
                or nameof(PuppetDeformationItemViewModel.BoneId))
            {
                InvalidateVisual();
            }
        }

        void Pin_RestChanged(object? sender, EventArgs e)
        {
            //ピンが動くとコンテンツ範囲が変わるためスクロール量も更新する
            UpdateScrollInfo();
            InvalidateVisual();
        }

        #region 座標変換

        //レイアウト計算の中間結果。フィット×ズームの実スケールと、スクロール範囲の元になるコンテンツ矩形を持つ。
        readonly record struct ViewMetrics(
            double Scale, Rect ContentLocal, double ImageWidth, double ImageHeight,
            double ViewportW, double ViewportH, double ExtentW, double ExtentH);

        ViewMetrics? GetMetrics()
        {
            var image = viewModel?.CanvasImage;
            if (image is null || RenderSize.Width <= 0 || RenderSize.Height <= 0)
                return null;

            double iw = image.PixelWidth;
            double ih = image.PixelHeight;
            if (iw <= 0 || ih <= 0)
                return null;

            double vpW = RenderSize.Width, vpH = RenderSize.Height;
            //ズーム1.0＝画像全体がちょうど収まるフィット表示
            var fitScale = Math.Min(vpW / iw, vpH / ih);
            var scale = fitScale * zoom;

            //画像とすべてのピン/ジョイントを含む範囲。画面外のピンもスクロールで辿れるようにする。
            //余白は付けない（フィット表示かつ画面外ピンが無ければスクロールの余地=0となり、バーを出さない）。
            var content = GetContentLocalBounds(iw, ih);

            return new ViewMetrics(scale, content, iw, ih, vpW, vpH, content.Width * scale, content.Height * scale);
        }

        //画像矩形とすべてのピン/ジョイントを内包するローカル座標の範囲（画像中心を原点とする座標系）
        Rect GetContentLocalBounds(double iw, double ih)
        {
            double minX = -iw * 0.5, minY = -ih * 0.5, maxX = iw * 0.5, maxY = ih * 0.5;
            foreach (var pin in pins)
            {
                var p = GetRestPoint(pin);
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            foreach (var bone in bones)
            {
                var j = GetJointPoint(bone);
                if (j.X < minX) minX = j.X;
                if (j.Y < minY) minY = j.Y;
                if (j.X > maxX) maxX = j.X;
                if (j.Y > maxY) maxY = j.Y;
            }
            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }

        (double Scale, Point Origin, double ImageWidth, double ImageHeight)? GetLayout()
        {
            var metrics = GetMetrics();
            if (metrics is null)
                return null;
            var m = metrics.Value;

            //コンテンツがビューポートより小さいときは中央に寄せる
            double padX = Math.Max(0, (m.ViewportW - m.ExtentW) * 0.5);
            double padY = Math.Max(0, (m.ViewportH - m.ExtentH) * 0.5);
            double offX = Math.Clamp(ScrollOffsetX, 0, Math.Max(0, m.ExtentW - m.ViewportW));
            double offY = Math.Clamp(ScrollOffsetY, 0, Math.Max(0, m.ExtentH - m.ViewportH));

            //LocalToDisplayの式（origin + (local + 画像半分)*scale）が、コンテンツ左上をスクロール位置に一致させるようoriginを決める
            double originX = padX - offX - (m.ContentLocal.Left + m.ImageWidth * 0.5) * m.Scale;
            double originY = padY - offY - (m.ContentLocal.Top + m.ImageHeight * 0.5) * m.Scale;
            return (m.Scale, new Point(originX, originY), m.ImageWidth, m.ImageHeight);
        }

        /// <summary>ズーム/パンの結果に合わせてスクロールバーの範囲・可視性を更新する。全体表示への予約があれば中央寄せする。</summary>
        void UpdateScrollInfo()
        {
            var metrics = GetMetrics();
            if (metrics is null)
            {
                ScrollMaxX = 0;
                ScrollMaxY = 0;
                ScrollViewportW = 0;
                ScrollViewportH = 0;
                HScrollVisibility = Visibility.Collapsed;
                VScrollVisibility = Visibility.Collapsed;
                return;
            }
            var m = metrics.Value;
            double maxX = Math.Max(0, m.ExtentW - m.ViewportW);
            double maxY = Math.Max(0, m.ExtentH - m.ViewportH);

            //先に最大値を反映してからオフセットを設定する（オフセットは最大値でクランプされるため）
            ScrollMaxX = maxX;
            ScrollMaxY = maxY;
            ScrollViewportW = m.ViewportW;
            ScrollViewportH = m.ViewportH;
            HScrollVisibility = maxX > 0.5 ? Visibility.Visible : Visibility.Collapsed;
            VScrollVisibility = maxY > 0.5 ? Visibility.Visible : Visibility.Collapsed;

            if (pendingResetView)
            {
                //画像中心をビューポート中心に合わせるスクロール位置
                double padX = Math.Max(0, (m.ViewportW - m.ExtentW) * 0.5);
                double padY = Math.Max(0, (m.ViewportH - m.ExtentH) * 0.5);
                double centerOffX = padX - m.ContentLocal.Left * m.Scale - m.ViewportW * 0.5;
                double centerOffY = padY - m.ContentLocal.Top * m.Scale - m.ViewportH * 0.5;
                //バインディングを壊さないようSetCurrentValueで設定する
                SetCurrentValue(ScrollOffsetXProperty, Math.Clamp(centerOffX, 0, maxX));
                SetCurrentValue(ScrollOffsetYProperty, Math.Clamp(centerOffY, 0, maxY));
                pendingResetView = false;
            }
        }

        /// <summary>ズームを等倍に戻し、画像を中央に表示する（全体表示に戻す）。</summary>
        public void ResetView()
        {
            zoom = 1.0;
            pendingResetView = true;
            UpdateScrollInfo();
            InvalidateVisual();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            //サイズ確定でビューポートが変わるため、スクロール範囲と（保留中なら）中央寄せを更新する
            UpdateScrollInfo();
            InvalidateVisual();
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

        static Point GetJointPoint(PuppetBoneViewModel bone)
            => new(
                bone.Model.JointX.Values.FirstOrDefault()?.Value ?? 0,
                bone.Model.JointY.Values.FirstOrDefault()?.Value ?? 0);

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

            //編集中の対象を手前に描画する。ピン編集モード時はボーンを半透明にして脇役に回す
            if (IsBoneMode)
            {
                DrawPins(drawingContext, l);
                DrawBones(drawingContext, l);
            }
            else
            {
                drawingContext.PushOpacity(InactiveBoneOpacity);
                DrawBones(drawingContext, l);
                drawingContext.Pop();
                DrawPins(drawingContext, l);
            }
        }

        void DrawPins(DrawingContext drawingContext, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout)
        {
            //ボーンモード時、選択中ボーンに割り当てられたピンをリングで示す
            var selectedBone = IsBoneMode ? bones.FirstOrDefault(b => b.IsSelected) : null;

            foreach (var pin in pins)
            {
                var p = LocalToDisplay(GetRestPoint(pin), layout);
                //マウスオーバー中は少し大きく描いてカーソルが当たっていることを示す
                var radius = pin == hoveredPin ? PinRadius + HoverRadiusBonus : PinRadius;

                //ピンの選択リングはピン編集モードでのみ表示する
                if (!IsBoneMode && pin.IsRestSelected)
                    drawingContext.DrawEllipse(null, PinSelectionPen, p, radius + 3.5, radius + 3.5);
                if (selectedBone is not null && pin.BoneId == selectedBone.Model.Id)
                    drawingContext.DrawEllipse(null, AssignedPinPen, p, radius + 3.5, radius + 3.5);

                drawingContext.DrawEllipse(null, PinHaloPen, p, radius, radius);
                drawingContext.DrawEllipse(pin.IsEnabled ? PinFillBrush : DisabledPinFillBrush, PinStrokePen, p, radius, radius);
            }
        }

        void DrawBones(DrawingContext drawingContext, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout)
        {
            if (bones.Count == 0)
                return;

            //割当ピンとの接続点線（一番下に描く）
            foreach (var pin in pins)
            {
                if (pin.BoneId == Guid.Empty)
                    continue;
                var bone = bones.FirstOrDefault(b => b.Model.Id == pin.BoneId);
                if (bone is null)
                    continue;
                var from = LocalToDisplay(GetJointPoint(bone), layout);
                var to = LocalToDisplay(GetRestPoint(pin), layout);
                drawingContext.DrawLine(BonePinLinkPen, from, to);
            }

            //親子の接続線（ジョイントの下に描く）
            foreach (var bone in bones)
            {
                if (bone.Model.ParentId == Guid.Empty)
                    continue;
                var parent = bones.FirstOrDefault(b => b.Model.Id == bone.Model.ParentId && b != bone);
                if (parent is null)
                    continue;
                var from = LocalToDisplay(GetJointPoint(parent), layout);
                var to = LocalToDisplay(GetJointPoint(bone), layout);
                drawingContext.DrawLine(BoneLinkPen, from, to);

                //親→子の向きを線の中間の矢じりで示す（短すぎるリンクには描かない）
                var arrow = CreateArrowGeometry(from, to, BoneArrowSize);
                if (arrow is not null)
                    drawingContext.DrawGeometry(BoneDirectionBrush, BoneDirectionPen, arrow);
            }

            //ジョイント挿入のプレビュー：ホバー中の親子リンクを強調し、挿入位置に半透明のジョイントを描く
            if (IsBoneMode && hoveredSegmentBone is not null)
            {
                var child = hoveredSegmentBone;
                var parent = bones.FirstOrDefault(b => b.Model.Id == child.Model.ParentId && b != child);
                if (parent is not null)
                {
                    var from = LocalToDisplay(GetJointPoint(parent), layout);
                    var to = LocalToDisplay(GetJointPoint(child), layout);
                    drawingContext.DrawLine(BoneSegmentHighlightPen, from, to);
                    var ghost = CreateDiamondGeometry(hoveredSegmentPoint, BoneRadius);
                    drawingContext.DrawGeometry(BoneInsertGhostBrush, PinStrokePen, ghost);
                }
            }

            //ジョイント（ひし形）
            foreach (var bone in bones)
            {
                var p = LocalToDisplay(GetJointPoint(bone), layout);
                //マウスオーバー中は少し大きく描いてカーソルが当たっていることを示す
                var radius = bone == hoveredBone ? BoneRadius + HoverRadiusBonus : BoneRadius;

                //ジョイントの選択リングはボーン編集モードでのみ表示する
                if (IsBoneMode && bone.IsSelected)
                    drawingContext.DrawEllipse(null, BoneSelectionPen, p, radius + 3.5, radius + 3.5);

                var diamond = CreateDiamondGeometry(p, radius);
                drawingContext.DrawGeometry(null, PinHaloPen, diamond);
                drawingContext.DrawGeometry(bone.IsEnabled ? BoneFillBrush : DisabledBoneFillBrush, PinStrokePen, diamond);

                //親を持たないルートジョイントは外周のひし形リングで区別する（選択リングは円なので形で見分けられる）
                if (bone.Model.ParentId == Guid.Empty)
                {
                    var rootRing = CreateDiamondGeometry(p, radius + BoneRootRingMargin);
                    drawingContext.DrawGeometry(null, BoneRootRingPen, rootRing);
                }
            }
        }

        /// <summary>
        /// 親子リンクの中点に、親(from)→子(to)の向きを指す矢じり（三角形）を作る。
        /// リンクが矢じりを収められないほど短い場合はnullを返す。
        /// </summary>
        static StreamGeometry? CreateArrowGeometry(Point from, Point to, double size)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            //矢じり全長（2*size）が収まらないほど短いリンクには描かない
            if (length < size * 2.5)
                return null;

            var ux = dx / length;
            var uy = dy / length;
            //進行方向に直交する単位ベクトル
            var px = -uy;
            var py = ux;
            var mid = new Point((from.X + to.X) * 0.5, (from.Y + to.Y) * 0.5);
            var tip = new Point(mid.X + ux * size, mid.Y + uy * size);
            var baseCenter = new Point(mid.X - ux * size, mid.Y - uy * size);
            var half = size * 0.75;
            var left = new Point(baseCenter.X + px * half, baseCenter.Y + py * half);
            var right = new Point(baseCenter.X - px * half, baseCenter.Y - py * half);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(tip, true, true);
                ctx.LineTo(left, true, false);
                ctx.LineTo(right, true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        static StreamGeometry CreateDiamondGeometry(Point center, double radius)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(center.X, center.Y - radius), true, true);
                ctx.LineTo(new Point(center.X + radius, center.Y), true, false);
                ctx.LineTo(new Point(center.X, center.Y + radius), true, false);
                ctx.LineTo(new Point(center.X - radius, center.Y), true, false);
            }
            geometry.Freeze();
            return geometry;
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

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            //Ctrl+ホイールのみズーム。修飾なしのホイールは外側のスクロール（プロパティ一覧）に委ねる
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;
            if (e.Delta == 0)
                return;

            var layout = GetLayout();
            if (layout is null)
                return;

            var cursor = e.GetPosition(this);
            //ズーム前にカーソル下のローカル座標を控え、ズーム後も同じ点がカーソル下に来るようスクロール位置を調整する
            var anchor = DisplayToLocal(cursor, layout.Value);

            var newZoom = Math.Clamp(zoom * (e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep), MinZoom, MaxZoom);
            if (newZoom == zoom)
            {
                e.Handled = true;
                return;
            }
            zoom = newZoom;
            UpdateScrollInfo();

            var layout2 = GetLayout();
            if (layout2 is not null)
            {
                //新スケールでのアンカーの表示位置を求め、カーソル位置とのズレをスクロールで打ち消す
                var after = LocalToDisplay(anchor, layout2.Value);
                SetCurrentValue(ScrollOffsetXProperty, Math.Clamp(ScrollOffsetX + (after.X - cursor.X), 0, ScrollMaxX));
                SetCurrentValue(ScrollOffsetYProperty, Math.Clamp(ScrollOffsetY + (after.Y - cursor.Y), 0, ScrollMaxY));
            }
            InvalidateVisual();
            e.Handled = true;
        }

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
            pressedOnSelectedBone = false;

            if (IsBoneMode)
            {
                OnBoneModeLeftButtonDown(pos, layout.Value, e);
                return;
            }

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
            isBoneDragging = false;
            dragMoved = false;
            lastDragPosition = pos;
            CaptureMouse();
            e.Handled = true;
        }

        void OnBoneModeLeftButtonDown(Point pos, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout, MouseButtonEventArgs e)
        {
            if (viewModel is null)
                return;

            var hitBone = HitTestBone(pos, layout);
            if (hitBone is not null)
            {
                //選択済みジョイントをドラッグせずクリックした場合、マウスアップ時に選択解除する
                pressedOnSelectedBone = hitBone.IsSelected;
                viewModel.SelectBoneFromCanvas(hitBone);
            }
            else
            {
                //ピンをクリックしたら選択中ボーンへの割り当てを切り替える
                var hitPin = HitTestPin(pos, layout);
                if (hitPin is not null)
                {
                    viewModel.TogglePinBoneAssignFromCanvas(hitPin);
                    e.Handled = true;
                    return;
                }

                if (!viewModel.CanAddBone)
                    return;

                //親子リンク（線分）の上ならジョイントを挿入して分割、そうでなければ新規ボーンを追加する
                var segment = HitTestBoneSegment(pos, layout);
                if (segment is not null)
                {
                    var insertLocal = DisplayToLocal(segment.Value.Display, layout);
                    viewModel.InsertBoneOnSegmentFromCanvas(segment.Value.Bone, insertLocal.X, insertLocal.Y);
                }
                else
                {
                    var local = DisplayToLocal(pos, layout);
                    viewModel.AddBoneFromCanvas(local.X, local.Y);
                }

                //ボーン一覧の再構築でホバー対象のViewModelは破棄されるため、参照が残らないようクリアする
                ClearHover();

                //追加でボーン一覧が再構築されるため、追加されたボーンを取り直してそのままドラッグできるようにする
                hitBone = HitTestBone(pos, layout);
                if (hitBone is null)
                    return;
            }

            isDragging = true;
            isBoneDragging = true;
            dragMoved = false;
            lastDragPosition = pos;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (isPanning)
            {
                var p = e.GetPosition(this);
                var dxp = p.X - lastPanPosition.X;
                var dyp = p.Y - lastPanPosition.Y;
                //わずかな移動は中クリック（削除）とみなし、しきい値を超えて初めてパンを開始する
                if (!panMoved && Math.Abs(dxp) < PanThreshold && Math.Abs(dyp) < PanThreshold)
                    return;
                panMoved = true;
                //ドラッグ方向にコンテンツが動く＝スクロール位置は逆方向へ動かす
                SetCurrentValue(ScrollOffsetXProperty, Math.Clamp(ScrollOffsetX - dxp, 0, ScrollMaxX));
                SetCurrentValue(ScrollOffsetYProperty, Math.Clamp(ScrollOffsetY - dyp, 0, ScrollMaxY));
                lastPanPosition = p;
                InvalidateVisual();
                return;
            }

            if (viewModel is null)
                return;
            var layout = GetLayout();
            if (layout is null || layout.Value.Scale <= 0)
                return;

            var pos = e.GetPosition(this);

            if (!isDragging)
            {
                UpdateHover(pos, layout.Value);
                return;
            }
            var dx = (pos.X - lastDragPosition.X) / layout.Value.Scale;
            var dy = (pos.Y - lastDragPosition.Y) / layout.Value.Scale;
            if (dx == 0 && dy == 0)
                return;

            if (!dragMoved)
            {
                if (isBoneDragging)
                    viewModel.BeginBoneDragFromCanvas();
                else
                    viewModel.BeginRestDragFromCanvas();
                dragMoved = true;
            }
            if (isBoneDragging)
                viewModel.MoveSelectedBonesFromCanvas(dx, dy);
            else
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
            //選択済みジョイントをドラッグせずクリックした場合は選択解除する
            if (isBoneDragging && pressedOnSelectedBone && !dragMoved)
                viewModel?.ClearBoneSelectionFromCanvas();
            pressedOnSelectedBone = false;
            EndActiveDrag();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (isPanning)
            {
                isPanning = false;
                panMoved = false;
            }
            if (!isDragging)
                return;
            isDragging = false;
            pressedOnSelectedBone = false;
            EndActiveDrag();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            ClearHover();
        }

        void UpdateHover(Point pos, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout)
        {
            PuppetBoneViewModel? bone = null;
            PuppetDeformationItemViewModel? pin = null;
            PuppetBoneViewModel? segmentBone = null;
            var segmentPoint = default(Point);
            if (IsBoneMode)
            {
                //ボーンモードではジョイント→ピン→親子リンクの順でホバー対象を判定する
                bone = HitTestBone(pos, layout);
                if (bone is null)
                {
                    pin = HitTestPin(pos, layout);
                    if (pin is null)
                    {
                        var segment = HitTestBoneSegment(pos, layout);
                        if (segment is not null)
                        {
                            segmentBone = segment.Value.Bone;
                            segmentPoint = segment.Value.Display;
                        }
                    }
                }
            }
            else
            {
                pin = HitTestPin(pos, layout);
            }

            if (bone == hoveredBone && pin == hoveredPin && segmentBone == hoveredSegmentBone && segmentPoint == hoveredSegmentPoint)
                return;
            hoveredBone = bone;
            hoveredPin = pin;
            hoveredSegmentBone = segmentBone;
            hoveredSegmentPoint = segmentPoint;
            InvalidateVisual();
        }

        void ClearHover()
        {
            if (hoveredBone is null && hoveredPin is null && hoveredSegmentBone is null)
                return;
            hoveredBone = null;
            hoveredPin = null;
            hoveredSegmentBone = null;
            InvalidateVisual();
        }

        void EndActiveDrag()
        {
            if (dragMoved)
            {
                if (isBoneDragging)
                    viewModel?.EndBoneDragFromCanvas();
                else
                    viewModel?.EndRestDragFromCanvas();
            }
            dragMoved = false;
            isBoneDragging = false;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            if (TryRemoveTargetAt(e.GetPosition(this)))
            {
                e.Handled = true;
                return;
            }
            //背景の右クリックはジョイントの選択解除
            viewModel?.ClearBoneSelectionFromCanvas();
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            //中ボタン: ドラッグで表示位置を移動（パン）、ドラッグせず離したら削除として扱う（左右ボタンは専用ハンドラ側で処理する）
            if (e.ChangedButton != MouseButton.Middle)
                return;
            //左ドラッグ中は割り込ませない
            if (isDragging)
                return;
            isPanning = true;
            panMoved = false;
            lastPanPosition = e.GetPosition(this);
            Focus();
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton != MouseButton.Middle || !isPanning)
                return;
            isPanning = false;
            ReleaseMouseCapture();
            //ドラッグしていなければ中クリック＝削除（背景ならジョイントの選択解除）として扱う
            if (!panMoved)
            {
                if (!TryRemoveTargetAt(e.GetPosition(this)))
                    viewModel?.ClearBoneSelectionFromCanvas();
            }
            panMoved = false;
            e.Handled = true;
        }

        bool TryRemoveTargetAt(Point display)
        {
            if (viewModel is null)
                return false;
            var layout = GetLayout();
            if (layout is null)
                return false;

            if (IsBoneMode)
            {
                var hitBone = HitTestBone(display, layout.Value);
                if (hitBone is null)
                    return false;
                viewModel.RemoveBoneFromCanvas(hitBone);
                return true;
            }

            var hit = HitTestPin(display, layout.Value);
            if (hit is null)
                return false;
            viewModel.RemovePinFromCanvas(hit);
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (viewModel is null)
                return;
            if (e.Key == Key.Delete)
            {
                if (IsBoneMode)
                    viewModel.RemoveSelectedBoneFromCanvas();
                else
                    viewModel.RemoveSelectedRestPinsFromCanvas();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && IsBoneMode)
            {
                //選択解除するとルートボーンを追加できる
                viewModel.ClearBoneSelectionFromCanvas();
                e.Handled = true;
            }
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

        PuppetBoneViewModel? HitTestBone(Point display, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout)
        {
            PuppetBoneViewModel? nearest = null;
            var nearestDistSq = BoneHitRadius * BoneHitRadius;
            //後に描画される（手前の）ボーンを優先する
            for (var i = bones.Count - 1; i >= 0; i--)
            {
                var p = LocalToDisplay(GetJointPoint(bones[i]), layout);
                var dx = p.X - display.X;
                var dy = p.Y - display.Y;
                var distSq = dx * dx + dy * dy;
                if (distSq <= nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = bones[i];
                }
            }
            return nearest;
        }

        /// <summary>
        /// 親子リンク（線分）へのヒットテスト。ヒットした場合は分割対象の子ボーンと、
        /// 線分上に投影した挿入位置（表示座標）を返す。ジョイント自体はここでは対象にしない。
        /// </summary>
        (PuppetBoneViewModel Bone, Point Display)? HitTestBoneSegment(Point display, (double Scale, Point Origin, double ImageWidth, double ImageHeight) layout)
        {
            PuppetBoneViewModel? nearest = null;
            var nearestProjection = default(Point);
            var nearestDistSq = BoneSegmentHitRadius * BoneSegmentHitRadius;
            //後に描画される（手前の）ボーンのリンクを優先する
            for (var i = bones.Count - 1; i >= 0; i--)
            {
                var bone = bones[i];
                if (bone.Model.ParentId == Guid.Empty)
                    continue;
                var parent = bones.FirstOrDefault(b => b.Model.Id == bone.Model.ParentId && b != bone);
                if (parent is null)
                    continue;
                var a = LocalToDisplay(GetJointPoint(parent), layout);
                var b2 = LocalToDisplay(GetJointPoint(bone), layout);
                var projection = ClosestPointOnSegment(display, a, b2);
                var dx = projection.X - display.X;
                var dy = projection.Y - display.Y;
                var distSq = dx * dx + dy * dy;
                if (distSq <= nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = bone;
                    nearestProjection = projection;
                }
            }
            return nearest is null ? null : (nearest, nearestProjection);
        }

        static Point ClosestPointOnSegment(Point p, Point a, Point b)
        {
            var abx = b.X - a.X;
            var aby = b.Y - a.Y;
            var lengthSq = abx * abx + aby * aby;
            if (lengthSq <= 1e-9)
                return a;
            var t = Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lengthSq, 0.0, 1.0);
            return new Point(a.X + abx * t, a.Y + aby * t);
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

        static Pen CreateFrozenDashedPen(Color color, double thickness)
        {
            var pen = new Pen(CreateFrozenBrush(color), thickness) { DashStyle = DashStyles.Dash };
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

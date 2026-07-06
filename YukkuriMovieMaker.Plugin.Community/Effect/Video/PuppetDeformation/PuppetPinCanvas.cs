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
                InvalidateVisual();
            }
            else if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.CanvasPins))
            {
                DetachPins();
                AttachPins();
                ClearHover();
                InvalidateVisual();
            }
            else if (e.PropertyName == nameof(PuppetDeformationListEditorViewModel.CanvasBones))
            {
                DetachBones();
                AttachBones();
                ClearHover();
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

        void Bone_VisualChanged(object? sender, EventArgs e) => InvalidateVisual();

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
            }
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
            //中クリックでも削除できるようにする（左右ボタンは専用ハンドラ側で処理する）
            if (e.ChangedButton != MouseButton.Middle)
                return;
            if (TryRemoveTargetAt(e.GetPosition(this)))
            {
                e.Handled = true;
                return;
            }
            //背景の中クリックはジョイントの選択解除
            viewModel?.ClearBoneSelectionFromCanvas();
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

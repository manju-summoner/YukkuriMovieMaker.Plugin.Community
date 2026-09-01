using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointListEditorViewModel : Bindable, IDisposable
    {
        ImmutableList<VectorFieldPointItemViewModel> allViewModels = ImmutableList<VectorFieldPointItemViewModel>.Empty;

        object? selectedTarget;
        VectorFieldPointItemViewModel? selectedItem;

        bool isMutatingSelection;
        volatile bool disposedValue;

        IEditorInfo? editorInfo;
        bool isCanvasImageInitialized;
        int lastCanvasImageFrame = -1;
        int lastReceivedEditorFrame = -1;
        readonly object canvasImageRefreshLock = new();
        CanvasImageRequest? pendingCanvasImageRequest;
        bool isCanvasImageRefreshRunning;
        int canvasImageRequestId;
        CancellationTokenSource? frameRefreshDebounceCts;

        readonly AnimationWatcher amountWatcher;
        readonly AnimationWatcher maxDisplacementWatcher;

        //フレーム変化起因の再描画を遅延させる時間。
        //再生・シーク中はフレーム変化が連続するため、変化が止まるまで再描画を抑止する
        //（アイテム画像のフル再レンダリングはグループ制御などでGPU負荷が大きく、毎フレーム実行するとGPUハングの原因になる）
        internal int FrameRefreshDebounceMilliseconds { get; set; } = 300;

        public void SetEditorInfo(IEditorInfo? info)
        {
            editorInfo = info;
            //エディタのデタッチ時などにnullが渡される
            if (info is null)
            {
                CancelPendingDebouncedRefresh();
                InvalidateCanvasImageRequests();
                return;
            }
            var frame = info.ItemPosition.Frame;
            if (isCanvasImageInitialized)
            {
                //ループ再生では描画済みフレームへ戻る遷移があるため、遷移の検知は「直前に受信したフレーム」と比較する
                //（描画済みフレームとの比較だと、そのフレームを通過するたびにデバウンスを迂回してしまう）
                if (frame != lastReceivedEditorFrame)
                    ScheduleDebouncedCanvasImageRefresh();
                else
                    //同一フレームでの編集（Undo/Redo等）の反映は即時に行う
                    QueueCanvasImageRefresh();
                lastReceivedEditorFrame = frame;
                return;
            }
            isCanvasImageInitialized = true;
            lastReceivedEditorFrame = frame;
            QueueCanvasImageRefresh();
        }

        void ScheduleDebouncedCanvasImageRefresh()
        {
            if (disposedValue)
                return;
            var newCts = new CancellationTokenSource();
            var token = newCts.Token;
            CancelAndDispose(Interlocked.Exchange(ref frameRefreshDebounceCts, newCts));
            //Disposeと競合したら自分で片付ける
            if (disposedValue)
            {
                CancelAndDispose(Interlocked.Exchange(ref frameRefreshDebounceCts, null));
                return;
            }
            _ = DebounceCanvasImageRefreshAsync(token);
        }

        async Task DebounceCanvasImageRefreshAsync(CancellationToken token)
        {
            //未観測例外はリリースビルドでアプリを終了させるため、本体全体をtryで覆う
            try
            {
                await Task.Delay(FrameRefreshDebounceMilliseconds, token);
                if (disposedValue || token.IsCancellationRequested)
                    return;
                //即時経路のQueueCanvasImageRefreshは自分の予約を取り消すため、発火側はCoreを直接呼んで競合を避ける
                QueueCanvasImageRefreshCore();
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        void CancelPendingDebouncedRefresh()
            => CancelAndDispose(Interlocked.Exchange(ref frameRefreshDebounceCts, null));

        static void CancelAndDispose(CancellationTokenSource? cts)
        {
            if (cts is null)
                return;
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            cts.Dispose();
        }

        public ImageSource? CanvasImage { get => canvasImage; private set => Set(ref canvasImage, value); }
        ImageSource? canvasImage;

        public Size CanvasImageSize { get => canvasImageSize; private set => Set(ref canvasImageSize, value); }
        Size canvasImageSize = Size.Empty;

        public Size CanvasBaseSize { get => canvasBaseSize; private set => Set(ref canvasBaseSize, value); }
        Size canvasBaseSize = Size.Empty;

        public Rect CanvasBaseBounds { get => canvasBaseBounds; private set => Set(ref canvasBaseBounds, value); }
        Rect canvasBaseBounds = Rect.Empty;

        public Rect CanvasImageBounds { get => canvasImageBounds; private set => Set(ref canvasImageBounds, value); }
        Rect canvasImageBounds = Rect.Empty;

        /// <summary>キャンバス画像のピクセル数／ローカル座標上のサイズの比。縮小レンダリング時は1未満になる。</summary>
        public double CanvasImageScale { get => canvasImageScale; private set => Set(ref canvasImageScale, value); }
        double canvasImageScale = 1.0;

        public ImmutableList<VectorFieldPointItemViewModel> CanvasPoints { get => canvasPoints; private set => Set(ref canvasPoints, value); }
        ImmutableList<VectorFieldPointItemViewModel> canvasPoints = ImmutableList<VectorFieldPointItemViewModel>.Empty;

        public object? SelectedTarget { get => selectedTarget; private set => Set(ref selectedTarget, value, nameof(SelectedTarget), nameof(HasNoSelection)); }

        public bool HasNoSelection => selectedTarget is null;

        public string NoSelectionMessage => Texts.VectorFieldWarpNoPointSelected;

        public bool CanAddPoint => Effect.Points.Count < VectorFieldWarpCustomEffect.MaxPoints;

        public ICommand AddPointCommand { get; }
        public ICommand RemovePointCommand { get; }
        public ICommand RefreshImageCommand { get; }
        public ICommand OnBeginEditPointCommand { get; }
        public ICommand OnEndEditPointCommand { get; }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[] ItemProperties { get; }

        VectorFieldWarpEffect Effect => (VectorFieldWarpEffect)ItemProperties[0].PropertyOwner;

        public VectorFieldPointListEditorViewModel(ItemProperty[] itemProperties)
        {
            ItemProperties = itemProperties;

            Effect.PropertyChanged += Effect_PropertyChanged;
            amountWatcher = new AnimationWatcher(Effect.Amount, QueueCanvasImageRefresh);
            maxDisplacementWatcher = new AnimationWatcher(Effect.MaxDisplacement, QueueCanvasImageRefresh);

            AddPointCommand = new ActionCommand(_ => CanAddPoint, _ => AddPointFromCanvas(0, 0));
            RemovePointCommand = new ActionCommand(_ => selectedItem != null, _ => RemoveSelectedPointsFromCanvas());
            RefreshImageCommand = new ActionCommand(_ => true, _ => QueueCanvasImageRefresh());
            OnBeginEditPointCommand = new ActionCommand(_ => true, _ => BeginEdit?.Invoke(this, EventArgs.Empty));
            OnEndEditPointCommand = new ActionCommand(_ => true, _ => EndEdit?.Invoke(this, EventArgs.Empty));

            RebuildViewModels();
        }

        void QueueCanvasImageRefresh()
        {
            //即時の再描画がデバウンス予約を追い越した場合、その後の発火で同じ内容を二重に描画しないよう予約を破棄する
            CancelPendingDebouncedRefresh();
            QueueCanvasImageRefreshCore();
        }

        void QueueCanvasImageRefreshCore()
        {
            var info = editorInfo;
            if (info is null || disposedValue)
                return;
            lastCanvasImageFrame = info.ItemPosition.Frame;

            var startWorker = false;
            lock (canvasImageRefreshLock)
            {
                pendingCanvasImageRequest = new CanvasImageRequest(info, Effect, ++canvasImageRequestId);
                if (!isCanvasImageRefreshRunning)
                {
                    isCanvasImageRefreshRunning = true;
                    startWorker = true;
                }
            }
            if (startWorker)
                _ = ProcessCanvasImageRequestsAsync();
        }

        void InvalidateCanvasImageRequests()
        {
            lock (canvasImageRefreshLock)
            {
                pendingCanvasImageRequest = null;
                canvasImageRequestId++;
            }
        }

        async Task ProcessCanvasImageRequestsAsync()
        {
            while (true)
            {
                CanvasImageRequest request;
                lock (canvasImageRefreshLock)
                {
                    if (pendingCanvasImageRequest is not CanvasImageRequest pending)
                    {
                        isCanvasImageRefreshRunning = false;
                        return;
                    }
                    request = pending;
                    pendingCanvasImageRequest = null;
                }

                var result = await Task.Run(() => LoadCanvasImage(request.Info, request.Effect));

                bool applyResult;
                bool hasNextRequest;
                lock (canvasImageRefreshLock)
                {
                    applyResult = !disposedValue && request.Id == canvasImageRequestId;
                    hasNextRequest = !disposedValue && pendingCanvasImageRequest is not null;
                    if (!hasNextRequest)
                        isCanvasImageRefreshRunning = false;
                }

                if (applyResult)
                    SetBaseImage(result);
                if (!hasNextRequest)
                    return;
            }
        }

        static CanvasImageResult LoadCanvasImage(IEditorInfo info, VectorFieldWarpEffect effect)
        {
            try
            {
                using var itemVideoSource = info.CreateItemVideoSource(
                    new ItemVideoSourceCreationParameter(VideoEffectSelection.UpToIncluding(effect)) { ApplyItemTransform = false });
                if (itemVideoSource is null)
                    return CanvasImageResult.Empty;

                var time = info.ItemPosition.Time;
                if (time < TimeSpan.Zero)
                    time = TimeSpan.Zero;
                else if (info.ItemDuration.Time <= time && info.ItemDuration.Frame > 0)
                    time = info.VideoInfo.GetTimeFrom(info.ItemDuration.Frame - 1);

                itemVideoSource.Update(time, Player.Video.TimelineSourceUsage.Paused);
                var image = itemVideoSource.RenderBitmapSource(out var bounds);
                //巨大なアイテムでは縮小レンダリングされ、画像のピクセル数とローカル座標上のサイズが一致しない。
                //ポイント座標と対応させるため、表示範囲はローカル座標のboundsから求める
                return new CanvasImageResult(image, CreateImageLocalBounds(bounds, image));
            }
            catch
            {
                return CanvasImageResult.Empty;
            }
        }

        void SetBaseImage(CanvasImageResult result)
        {
            var source = result.Image;
            if (source is null)
            {
                CanvasImage = null;
                CanvasImageSize = Size.Empty;
                CanvasBaseSize = Size.Empty;
                CanvasImageBounds = Rect.Empty;
                CanvasBaseBounds = Rect.Empty;
                CanvasImageScale = 1.0;
                return;
            }

            var size = new Size(source.PixelWidth, source.PixelHeight);
            //縮小レンダリング時のピクセル数とローカル座標の対応比。キャンバス側の座標変換に使う
            CanvasImageScale = ComputeCanvasImageScale(source.PixelWidth, source.PixelHeight, result.Bounds);
            CanvasImage = source;
            CanvasImageSize = size;
            CanvasBaseSize = size;
            CanvasImageBounds = result.Bounds;
            CanvasBaseBounds = result.Bounds;
        }

        /// <summary>
        /// キャンバス画像のピクセル数とローカル座標上のサイズの比を求める。
        /// 縮小レンダリングは両軸に同じ倍率を使うが、生成されるビットマップの各寸法は
        /// 切り上げと1px下限の補正を受けるため、比が歪みにくい長辺から算出する。
        /// </summary>
        internal static double ComputeCanvasImageScale(int pixelWidth, int pixelHeight, Rect bounds)
        {
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                return 1.0;
            return bounds.Width >= bounds.Height
                ? pixelWidth / bounds.Width
                : pixelHeight / bounds.Height;
        }

        static Rect CreateImageLocalBounds(Vortice.RawRectF bounds, BitmapSource image)
        {
            double left = bounds.Left;
            double top = bounds.Top;
            var width = bounds.Right - (double)bounds.Left;
            var height = bounds.Bottom - (double)bounds.Top;
            if (!double.IsFinite(left))
                left = 0;
            if (!double.IsFinite(top))
                top = 0;
            if (!double.IsFinite(width) || width <= 0)
                width = image.PixelWidth;
            if (!double.IsFinite(height) || height <= 0)
                height = image.PixelHeight;
            return new Rect(left, top, width, height);
        }

        readonly record struct CanvasImageRequest(IEditorInfo Info, VectorFieldWarpEffect Effect, int Id);
        readonly record struct CanvasImageResult(BitmapSource? Image, Rect Bounds)
        {
            public static CanvasImageResult Empty => new(null, Rect.Empty);
        }

        public double GetDisplayValue(Animation animation)
        {
            if (editorInfo is null)
                return animation.Values.FirstOrDefault()?.Value ?? 0;
            return animation.GetValue(editorInfo.ItemPosition.Frame, editorInfo.ItemDuration.Frame, editorInfo.VideoInfo.FPS);
        }

        public void SelectFromCanvas(VectorFieldPointItemViewModel vm, bool toggle)
        {
            isMutatingSelection = true;
            try
            {
                if (toggle)
                {
                    if (vm.IsSelected && allViewModels.Count(x => x.IsSelected) <= 1)
                        return;
                    vm.IsSelected = !vm.IsSelected;
                }
                else if (!vm.IsSelected)
                {
                    foreach (var item in allViewModels)
                        item.IsSelected = item == vm;
                }
            }
            finally
            {
                isMutatingSelection = false;
                UpdateSelection();
            }
        }

        public void AddPointFromCanvas(double x, double y)
        {
            if (!CanAddPoint)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var point in Effect.Points)
                point.IsSelected = false;
            var newPoint = VectorFieldPoint.Create(x, y);
            newPoint.IsSelected = true;
            CommitStructuralChange(Effect.Points.Add(newPoint));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void RemovePointFromCanvas(VectorFieldPointItemViewModel vm)
        {
            if (!Effect.Points.Contains(vm.Model))
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CommitStructuralChange(Effect.Points.Remove(vm.Model));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveSelectedPointsFromCanvas()
        {
            var targets = Effect.Points.Where(p => p.IsSelected).ToList();
            if (targets.Count == 0)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CommitStructuralChange(Effect.Points.RemoveRange(targets));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void BeginDragFromCanvas() => BeginEdit?.Invoke(this, EventArgs.Empty);

        public void MoveSelectedFromCanvas(double deltaX, double deltaY)
        {
            //複数アイテム選択時は、先頭アイテムの選択状態を基準に同じインデックスの制御点へ適用する
            var points = Effect.Points;
            for (var index = 0; index < points.Count; index++)
            {
                if (!points[index].IsSelected)
                    continue;
                foreach (var itemProperty in ItemProperties)
                {
                    if (itemProperty.PropertyOwner is not VectorFieldWarpEffect effect || index >= effect.Points.Count)
                        continue;
                    var point = effect.Points[index];
                    point.X.AddToEachValues(deltaX);
                    point.Y.AddToEachValues(deltaY);
                }
            }
        }

        public void EndDragFromCanvas() => EndEdit?.Invoke(this, EventArgs.Empty);

        void CommitStructuralChange(ImmutableList<VectorFieldPoint> newPoints)
        {
            //複数アイテム選択時は全アイテムへ反映する。Animation等の参照共有を避けるためアイテムごとにクローンする
            foreach (var itemProperty in ItemProperties)
            {
                var clones = newPoints.Select(p =>
                {
                    var clone = JsonConvert.DeserializeObject<VectorFieldPoint>(JsonConvert.SerializeObject(p))
                                ?? VectorFieldPoint.Create(0, 0);
                    clone.IsSelected = p.IsSelected;
                    return clone;
                }).ToImmutableList();
                itemProperty.SetValue(clones);
            }
        }

        void Effect_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VectorFieldWarpEffect.Points))
            {
                RebuildViewModels();
                OnPropertyChanged(nameof(CanAddPoint));
                QueueCanvasImageRefresh();
            }
            else if (e.PropertyName is nameof(VectorFieldWarpEffect.IntegrationSteps) or nameof(VectorFieldWarpEffect.IsEnabled))
            {
                QueueCanvasImageRefresh();
            }
        }

        void RebuildViewModels()
        {
            var points = Effect.Points;
            var existingByModel = allViewModels.ToDictionary(x => x.Model);
            var newAllViewModels = new List<VectorFieldPointItemViewModel>(points.Count);
            foreach (var point in points)
            {
                var vm = existingByModel.TryGetValue(point, out var existing)
                    ? existing
                    : new VectorFieldPointItemViewModel(point);
                newAllViewModels.Add(vm);
            }

            foreach (var oldVm in allViewModels.Except(newAllViewModels))
            {
                oldVm.PropertyChanged -= Item_PropertyChanged;
                oldVm.VisualChanged -= Item_VisualChanged;
                oldVm.Dispose();
            }

            foreach (var newVm in newAllViewModels.Except(allViewModels))
            {
                newVm.PropertyChanged += Item_PropertyChanged;
                newVm.VisualChanged += Item_VisualChanged;
            }

            allViewModels = ImmutableList.CreateRange(newAllViewModels);
            CanvasPoints = allViewModels;

            EnsureSelectionAfterRebuild();
            UpdateSelection();
        }

        void UpdateSelection()
        {
            if (isMutatingSelection) return;
            if (disposedValue) return;
            selectedItem = allViewModels.FirstOrDefault(x => x.IsSelected);
            SelectedTarget = selectedItem?.Model;
        }

        void EnsureSelectionAfterRebuild()
        {
            if (allViewModels.FirstOrDefault(x => x.IsSelected) != null) return;
            if (allViewModels.Count == 0) return;

            isMutatingSelection = true;
            try
            {
                allViewModels[0].IsSelected = true;
            }
            finally
            {
                isMutatingSelection = false;
            }
        }

        void Item_PropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(VectorFieldPointItemViewModel.IsSelected))
                UpdateSelection();
            else if (args.PropertyName == nameof(VectorFieldPointItemViewModel.IsEnabled))
                QueueCanvasImageRefresh();
        }

        void Item_VisualChanged(object? sender, EventArgs e)
        {
            QueueCanvasImageRefresh();
        }

        void Dispose(bool disposing)
        {
            if (disposedValue) return;
            disposedValue = true;
            CancelPendingDebouncedRefresh();
            InvalidateCanvasImageRequests();
            if (disposing)
            {
                amountWatcher.Dispose();
                maxDisplacementWatcher.Dispose();
                Effect.PropertyChanged -= Effect_PropertyChanged;
                foreach (var item in allViewModels)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.VisualChanged -= Item_VisualChanged;
                    item.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        sealed class AnimationWatcher : IDisposable
        {
            readonly Animation animation;
            readonly Action callback;
            //Animation.Valuesはリストごと置換されるため、購読時のリストを保持して確実に解除する
            ImmutableList<AnimationValue> subscribedValues = ImmutableList<AnimationValue>.Empty;

            public AnimationWatcher(Animation animation, Action callback)
            {
                this.animation = animation;
                this.callback = callback;
                animation.PropertyChanged += Animation_PropertyChanged;
                Subscribe();
            }

            void Subscribe()
            {
                subscribedValues = animation.Values;
                foreach (var value in subscribedValues)
                    value.PropertyChanged += Value_PropertyChanged;
            }

            void Unsubscribe()
            {
                foreach (var value in subscribedValues)
                    value.PropertyChanged -= Value_PropertyChanged;
                subscribedValues = ImmutableList<AnimationValue>.Empty;
            }

            void Animation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(Animation.Values) && e.PropertyName != nameof(Animation.AnimationType))
                    return;
                Unsubscribe();
                Subscribe();
                callback();
            }

            void Value_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                callback();
            }

            public void Dispose()
            {
                Unsubscribe();
                animation.PropertyChanged -= Animation_PropertyChanged;
            }
        }
    }
}

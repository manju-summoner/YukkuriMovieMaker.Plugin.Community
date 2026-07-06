using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal sealed class PuppetDeformationListEditorViewModel : Bindable, IDisposable
    {
        readonly ICommand selectRestCommand;
        readonly ICommand selectOffsetCommand;

        ImmutableList<PuppetDeformationItemViewModel> allViewModels = ImmutableList<PuppetDeformationItemViewModel>.Empty;

        object? selectedTarget;
        PuppetDeformationItemViewModel? selectedItem;
        PuppetDeformationEditMode editMode = PuppetDeformationEditMode.Pin;

        bool isMutatingSelection;
        bool disposedValue;

        EditSnapshot? activeSnapshot;

        IEditorInfo? editorInfo;
        bool isCanvasImageInitialized;

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;

            //SetEditorInfoはUndo履歴の更新などで編集操作のたびに呼ばれる。
            //毎回レンダリングするとデバイス生成が頻発するため、初回のみ取得し以降は更新ボタンに任せる
            if (isCanvasImageInitialized)
                return;
            isCanvasImageInitialized = true;
            RefreshCanvasImage();
        }

        /// <summary>ピン配置キャンバスに表示する画像（パペット変形の直前までのエフェクト適用済み）</summary>
        public System.Windows.Media.Imaging.BitmapSource? CanvasImage { get => canvasImage; private set => Set(ref canvasImage, value); }
        System.Windows.Media.Imaging.BitmapSource? canvasImage;

        /// <summary>ピン配置キャンバスに表示するピン一覧</summary>
        public ImmutableList<PuppetDeformationItemViewModel> CanvasPins { get => canvasPins; private set => Set(ref canvasPins, value); }
        ImmutableList<PuppetDeformationItemViewModel> canvasPins = ImmutableList<PuppetDeformationItemViewModel>.Empty;

        /// <summary>ピン配置キャンバスに表示するボーン一覧</summary>
        public ImmutableList<PuppetBoneViewModel> CanvasBones { get => canvasBones; private set => Set(ref canvasBones, value); }
        ImmutableList<PuppetBoneViewModel> canvasBones = ImmutableList<PuppetBoneViewModel>.Empty;

        public object? SelectedTarget { get => selectedTarget; set => Set(ref selectedTarget, value, nameof(SelectedTarget), nameof(HasNoSelection)); }

        /// <summary>ジョイント/ピンが選択されていない（プロパティエディタが空）かどうか</summary>
        public bool HasNoSelection => selectedTarget is null;

        /// <summary>選択が無いときにプロパティエディタの余白へ表示するメッセージ</summary>
        public string NoSelectionMessage => EditMode == PuppetDeformationEditMode.Bone
            ? Texts.PuppetDeformationNoJointSelected
            : Texts.PuppetDeformationNoPinSelected;

        public PuppetDeformationEditMode EditMode
        {
            get => editMode;
            set
            {
                if (Set(ref editMode, value, nameof(EditMode), nameof(IsPinMode), nameof(IsBoneMode), nameof(NoSelectionMessage)))
                    UpdateSelection();
            }
        }

        public bool IsPinMode
        {
            get => EditMode == PuppetDeformationEditMode.Pin;
            set { if (value) EditMode = PuppetDeformationEditMode.Pin; }
        }

        public bool IsBoneMode
        {
            get => EditMode == PuppetDeformationEditMode.Bone;
            set { if (value) EditMode = PuppetDeformationEditMode.Bone; }
        }

        public ICommand AddPinCommand { get; }
        public ICommand RemovePinCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AddBoneCommand { get; }
        public ICommand RemoveBoneCommand { get; }
        public ICommand RefreshImageCommand { get; }
        public ICommand OnBeginEditPointCommand { get; }
        public ICommand OnEndEditPointCommand { get; }

        public MessageBoxViewModel MessageBox { get; } = new MessageBoxViewModel();

        public bool CanAddPin => Effect.Pins.Count < PuppetDeformationCustomEffect.MaxPins;
        //ジョイントはピンと同じ拘束点枠(MaxPins)を消費するため、合計でも上限を超えないようにする
        public bool CanAddBone => Effect.Bones.Count < PuppetDeformationEffect.BoneCapacity
            && Effect.Pins.Count + Effect.Bones.Count < PuppetDeformationCustomEffect.MaxPins;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[] ItemProperties { get; internal set; }

        PuppetDeformationEffect Effect => (PuppetDeformationEffect)ItemProperties[0].PropertyOwner;

        public PuppetDeformationListEditorViewModel(ItemProperty[] itemProperties)
        {
            ItemProperties = itemProperties;

            Effect.PropertyChanged += Effect_PropertyChanged;

            selectRestCommand = new ActionCommand(_ => true, arg => HandleSelect(arg, isOffset: false));
            selectOffsetCommand = new ActionCommand(_ => true, arg => HandleSelect(arg, isOffset: true));

            AddPinCommand = new ActionCommand(_ => CanAddPin, _ =>
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                CommitStructuralChange(Effect.Pins.Add(PuppetDeformation.Create(0, 0)));
                EndEdit?.Invoke(this, EventArgs.Empty);
            });

            RemovePinCommand = new ActionCommand(_ => selectedItem != null, _ =>
            {
                if (selectedItem == null) return;
                var target = selectedItem.Model;
                BeginEdit?.Invoke(this, EventArgs.Empty);
                CommitStructuralChange(Effect.Pins.Remove(target));
                EndEdit?.Invoke(this, EventArgs.Empty);
            });

            ResetCommand = new ActionCommand(_ => Effect.Pins.Count > 0, _ =>
            {
                if (MessageBox.Show(Texts.PuppetDeformationListResetMessage, Texts.PuppetDeformationListResetTitle, MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                    return;
                BeginEdit?.Invoke(this, EventArgs.Empty);
                foreach (var pin in Effect.Pins)
                {
                    foreach (var v in pin.OffsetX.Values) v.Value = 0;
                    foreach (var v in pin.OffsetY.Values) v.Value = 0;
                }
                EndEdit?.Invoke(this, EventArgs.Empty);
            });

            AddBoneCommand = new ActionCommand(_ => CanAddBone, _ => AddBoneFromCanvas(0, 0));

            RemoveBoneCommand = new ActionCommand(
                _ => canvasBones.Any(b => b.IsSelected),
                _ => RemoveSelectedBoneFromCanvas());

            RefreshImageCommand = new ActionCommand(_ => true, _ => RefreshCanvasImage());

            OnBeginEditPointCommand = new ActionCommand(_ => true, _ => OnBeginEditPoint());
            OnEndEditPointCommand = new ActionCommand(_ => true, _ => OnEndEditPoint());

            RebuildViewModels();
            RebuildBoneViewModels();
        }

        void RefreshCanvasImage()
        {
            if (editorInfo is null)
                return;
            try
            {
                //生成されるBitmapSourceはCPU側の完全なコピー(frozen BitmapImage)のため、
                //デバイス一式を含むソースはレンダリング後すぐ破棄する。
                //保持するとエディタを開いたままアプリを終了した際に未解放オブジェクトとして残る
                using var itemVideoSource = editorInfo.CreateItemVideoSource(
                    new ItemVideoSourceCreationParameter(VideoEffectSelection.UpTo(Effect)));
                if (itemVideoSource is null)
                {
                    CanvasImage = null;
                    return;
                }

                var time = editorInfo.ItemPosition.Time;
                if (time < TimeSpan.Zero)
                    time = TimeSpan.Zero;
                else if (editorInfo.ItemDuration.Time <= time && editorInfo.ItemDuration.Frame > 0)
                    time = editorInfo.VideoInfo.GetTimeFrom(editorInfo.ItemDuration.Frame - 1);

                itemVideoSource.Update(time, Player.Video.TimelineSourceUsage.Paused);
                CanvasImage = itemVideoSource.RenderBitmapSource();
            }
            catch
            {
                CanvasImage = null;
            }
        }

        #region ピン配置キャンバス操作

        public void SelectRestFromCanvas(PuppetDeformationItemViewModel vm) => HandleSelect(vm, isOffset: false);

        public void AddPinFromCanvas(double restX, double restY)
        {
            if (!CanAddPin)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var pin in Effect.Pins)
            {
                pin.IsRestSelected = false;
                pin.IsOffsetSelected = false;
            }
            var newPin = PuppetDeformation.Create(restX, restY);
            newPin.IsRestSelected = true;
            CommitStructuralChange(Effect.Pins.Add(newPin));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void RemovePinFromCanvas(PuppetDeformationItemViewModel vm)
        {
            if (!Effect.Pins.Contains(vm.Model))
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CommitStructuralChange(Effect.Pins.Remove(vm.Model));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveSelectedRestPinsFromCanvas()
        {
            var targets = Effect.Pins.Where(p => p.IsRestSelected).ToList();
            if (targets.Count == 0)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CommitStructuralChange(Effect.Pins.RemoveRange(targets));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void BeginRestDragFromCanvas() => BeginEdit?.Invoke(this, EventArgs.Empty);

        public void MoveSelectedRestsFromCanvas(double deltaX, double deltaY)
        {
            foreach (var pin in Effect.Pins)
            {
                if (!pin.IsRestSelected)
                    continue;
                pin.RestX.AddToEachValues(deltaX);
                pin.RestY.AddToEachValues(deltaY);
            }
        }

        public void EndRestDragFromCanvas() => EndEdit?.Invoke(this, EventArgs.Empty);

        #endregion

        #region ボーン編集キャンバス操作

        public void SelectBoneFromCanvas(PuppetBoneViewModel vm)
        {
            isMutatingSelection = true;
            try
            {
                foreach (var b in canvasBones)
                    b.IsSelected = b == vm;
            }
            finally
            {
                isMutatingSelection = false;
                UpdateSelection();
            }
        }

        public void ClearBoneSelectionFromCanvas()
        {
            isMutatingSelection = true;
            try
            {
                foreach (var b in canvasBones)
                    b.IsSelected = false;
            }
            finally
            {
                isMutatingSelection = false;
                UpdateSelection();
            }
        }

        public void AddBoneFromCanvas(double jointX, double jointY)
        {
            if (!CanAddBone)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            //選択中のボーンを親にしてチェーンを作れるようにする
            var parent = Effect.Bones.FirstOrDefault(b => b.IsSelected);
            foreach (var b in Effect.Bones)
                b.IsSelected = false;
            var bone = PuppetBone.Create(jointX, jointY, parent?.Id ?? Guid.Empty);
            bone.IsSelected = true;
            CommitBonesChange(Effect.Bones.Add(bone));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 既存の親子リンク（線分）上に新しいジョイントを挿入してボーンを分割する。
        /// 新ジョイントは分割対象の子の親を引き継ぎ、子は新ジョイントに付け替える。
        /// </summary>
        public void InsertBoneOnSegmentFromCanvas(PuppetBoneViewModel childVm, double jointX, double jointY)
        {
            if (!CanAddBone)
                return;
            var child = childVm.Model;
            if (!Effect.Bones.Contains(child))
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var b in Effect.Bones)
                b.IsSelected = false;
            //新ジョイントは元の親の子として挿入し、既存の子をその下へ付け替える
            var inserted = PuppetBone.Create(jointX, jointY, child.ParentId);
            inserted.IsSelected = true;
            child.ParentId = inserted.Id;
            CommitBonesChange(Effect.Bones.Add(inserted));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveBoneFromCanvas(PuppetBoneViewModel vm) => RemoveBoneCore(vm.Model);

        public void RemoveSelectedBoneFromCanvas()
        {
            var target = Effect.Bones.FirstOrDefault(b => b.IsSelected);
            if (target is not null)
                RemoveBoneCore(target);
        }

        void RemoveBoneCore(PuppetBone target)
        {
            if (!Effect.Bones.Contains(target))
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            //子ボーンは削除ボーンの親へ付け替え、割当ピンは解除する
            foreach (var b in Effect.Bones)
            {
                if (b != target && b.ParentId == target.Id)
                    b.ParentId = target.ParentId;
            }
            foreach (var p in Effect.Pins)
            {
                if (p.BoneId == target.Id)
                    p.BoneId = Guid.Empty;
            }
            CommitBonesChange(Effect.Bones.Remove(target));
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        public void BeginBoneDragFromCanvas() => BeginEdit?.Invoke(this, EventArgs.Empty);

        public void MoveSelectedBonesFromCanvas(double deltaX, double deltaY)
        {
            foreach (var bone in Effect.Bones)
            {
                if (!bone.IsSelected)
                    continue;
                bone.JointX.AddToEachValues(deltaX);
                bone.JointY.AddToEachValues(deltaY);
            }
        }

        public void EndBoneDragFromCanvas() => EndEdit?.Invoke(this, EventArgs.Empty);

        /// <summary>選択中のボーンに対するピンの割り当てを切り替える</summary>
        public void TogglePinBoneAssignFromCanvas(PuppetDeformationItemViewModel pinVm)
        {
            var bone = Effect.Bones.FirstOrDefault(b => b.IsSelected);
            if (bone is null)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            var pin = pinVm.Model;
            pin.BoneId = pin.BoneId == bone.Id ? Guid.Empty : bone.Id;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        void CommitBonesChange(ImmutableList<PuppetBone> newBones)
        {
            var cloned = newBones.Select(b =>
            {
                var clone = JsonConvert.DeserializeObject<PuppetBone>(JsonConvert.SerializeObject(b)) ?? new PuppetBone();
                clone.IsSelected = b.IsSelected;
                return clone;
            }).ToImmutableList();
            Effect.Bones = cloned;
        }

        #endregion

        void CommitStructuralChange(ImmutableList<PuppetDeformation> newPins)
        {
            var cloned = newPins.Select(p =>
            {
                var clone = JsonConvert.DeserializeObject<PuppetDeformation>(JsonConvert.SerializeObject(p))
                            ?? PuppetDeformation.Create(0, 0);
                clone.IsRestSelected = p.IsRestSelected;
                clone.IsOffsetSelected = p.IsOffsetSelected;
                return clone;
            }).ToImmutableList();
            ItemProperties[0].SetValue(cloned);
        }

        void HandleSelect(object? arg, bool isOffset)
        {
            if (arg is not PuppetDeformationItemViewModel vm) return;

            isMutatingSelection = true;
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    SelectExclusively(vm, !isOffset);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (isOffset)
                    {
                        var next = !vm.IsOffsetSelected;
                        vm.IsOffsetSelected = next;
                        if (next) vm.IsRestSelected = false;
                    }
                    else
                    {
                        var next = !vm.IsRestSelected;
                        vm.IsRestSelected = next;
                        if (next) vm.IsOffsetSelected = false;
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    ExtendRangeSelection(vm, isOffset);
                }
                else
                {
                    SelectExclusively(vm, isOffset);
                }
            }
            finally
            {
                isMutatingSelection = false;
                UpdateSelection();
            }
        }

        void SelectExclusively(PuppetDeformationItemViewModel target, bool isOffset)
        {
            foreach (var item in allViewModels)
            {
                if (item == target) continue;
                item.IsRestSelected = false;
                item.IsOffsetSelected = false;
            }
            if (isOffset)
            {
                target.IsRestSelected = false;
                target.IsOffsetSelected = true;
            }
            else
            {
                target.IsOffsetSelected = false;
                target.IsRestSelected = true;
            }
        }

        void ExtendRangeSelection(PuppetDeformationItemViewModel target, bool isOffset)
        {
            var selectedIndices = allViewModels
                .Select((x, i) => (x, i))
                .Where(pair => pair.x.IsRestSelected || pair.x.IsOffsetSelected)
                .Select(pair => pair.i)
                .ToList();

            if (selectedIndices.Count == 0)
            {
                SelectExclusively(target, isOffset);
                return;
            }

            var min = selectedIndices.Min();
            var max = selectedIndices.Max();
            var targetIndex = allViewModels.IndexOf(target);
            var start = Math.Min(min, targetIndex);
            var end = Math.Max(max, targetIndex);

            for (var i = start; i <= end; i++)
            {
                var item = allViewModels[i];
                if (isOffset)
                {
                    item.IsRestSelected = false;
                    item.IsOffsetSelected = true;
                }
                else
                {
                    item.IsOffsetSelected = false;
                    item.IsRestSelected = true;
                }
            }
        }

        void Effect_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PuppetDeformationEffect.Pins))
            {
                RebuildViewModels();
                OnPropertyChanged(nameof(CanAddPin));
                OnPropertyChanged(nameof(CanAddBone));
            }
            else if (e.PropertyName == nameof(PuppetDeformationEffect.Bones))
            {
                RebuildBoneViewModels();
                OnPropertyChanged(nameof(CanAddBone));
            }
        }

        void RebuildBoneViewModels()
        {
            var bones = Effect.Bones;
            var existingByModel = canvasBones.ToDictionary(x => x.Model);
            var newViewModels = new List<PuppetBoneViewModel>(bones.Count);
            foreach (var bone in bones)
            {
                var vm = existingByModel.TryGetValue(bone, out var existing)
                         ? existing
                         : new PuppetBoneViewModel(bone);
                newViewModels.Add(vm);
            }

            foreach (var oldVm in canvasBones.Except(newViewModels))
            {
                oldVm.PropertyChanged -= Bone_PropertyChanged;
                oldVm.Dispose();
            }

            foreach (var newVm in newViewModels.Except(canvasBones))
            {
                newVm.PropertyChanged += Bone_PropertyChanged;
            }

            CanvasBones = ImmutableList.CreateRange(newViewModels);
            UpdateSelection();
        }

        void Bone_PropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(PuppetBoneViewModel.IsSelected))
                UpdateSelection();
        }

        void RebuildViewModels()
        {
            var pins = Effect.Pins;
            var existingByModel = allViewModels.ToDictionary(x => x.Model);
            var newAllViewModels = new List<PuppetDeformationItemViewModel>(pins.Count);
            foreach (var pin in pins)
            {
                var vm = existingByModel.TryGetValue(pin, out var existing)
                         ? existing
                         : new PuppetDeformationItemViewModel(pin, selectRestCommand, selectOffsetCommand);
                newAllViewModels.Add(vm);
            }

            foreach (var oldVm in allViewModels.Except(newAllViewModels))
            {
                oldVm.PropertyChanged -= Item_PropertyChanged;
                oldVm.Dispose();
            }

            foreach (var newVm in newAllViewModels.Except(allViewModels))
            {
                newVm.PropertyChanged += Item_PropertyChanged;
            }

            allViewModels = ImmutableList.CreateRange(newAllViewModels);
            CanvasPins = allViewModels;

            EnsureSelectionAfterRebuild();
            UpdateSelection();
        }

        void UpdateSelection()
        {
            if (isMutatingSelection) return;
            if (disposedValue) return;
            selectedItem = allViewModels.FirstOrDefault(x => x.IsRestSelected || x.IsOffsetSelected);
            var selectedBone = canvasBones.FirstOrDefault(x => x.IsSelected);
            SelectedTarget = EditMode == PuppetDeformationEditMode.Bone
                ? selectedBone?.Model
                : selectedItem?.Model;
        }

        void EnsureSelectionAfterRebuild()
        {
            if (allViewModels.FirstOrDefault(x => x.IsRestSelected || x.IsOffsetSelected) != null) return;
            if (allViewModels.Count == 0) return;

            isMutatingSelection = true;
            try
            {
                allViewModels[0].IsRestSelected = true;
            }
            finally
            {
                isMutatingSelection = false;
            }
        }

        void Item_PropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(PuppetDeformationItemViewModel.IsRestSelected)
                || args.PropertyName == nameof(PuppetDeformationItemViewModel.IsOffsetSelected))
            {
                UpdateSelection();
            }
        }

        void SyncRestValues()
        {
            if (selectedItem == null || activeSnapshot == null) return;

            var m = selectedItem.Model;
            var selectedPins = Effect.Pins.Where(x => x.IsRestSelected).ToList();
            if (selectedPins.Count <= 1) return;

            var changedAnimType = activeSnapshot.RestXAnimationType != m.RestX.AnimationType || activeSnapshot.RestYAnimationType != m.RestY.AnimationType;
            var changedSpan = activeSnapshot.RestXSpan != m.RestX.Span || activeSnapshot.RestYSpan != m.RestY.Span;

            if (changedAnimType)
            {
                foreach (var p in selectedPins)
                {
                    p.RestX.AnimationType = m.RestX.AnimationType;
                    p.RestY.AnimationType = m.RestY.AnimationType;
                }
                return;
            }
            if (changedSpan)
            {
                foreach (var p in selectedPins)
                {
                    p.RestX.Span = m.RestX.Span;
                    p.RestY.Span = m.RestY.Span;
                }
                return;
            }

            var changedXIndex = FindChangedValueIndex(activeSnapshot.RestXValues, m.RestX);
            var changedYIndex = FindChangedValueIndex(activeSnapshot.RestYValues, m.RestY);
            if (changedXIndex < 0 && changedYIndex < 0) return;

            foreach (var point in selectedPins.Where(p => p != m))
            {
                ApplyValueDelta(changedXIndex, m.RestX, point.RestX, activeSnapshot.RestXValues, 1f);
                ApplyValueDelta(changedYIndex, m.RestY, point.RestY, activeSnapshot.RestYValues, 1f);
            }
        }

        void SyncOffsetValues()
        {
            if (selectedItem == null || activeSnapshot == null) return;

            var m = selectedItem.Model;
            var selectedPins = Effect.Pins.Where(x => x.IsOffsetSelected).ToList();
            if (selectedPins.Count <= 1) return;

            var changedAnimType = activeSnapshot.OffsetXAnimationType != m.OffsetX.AnimationType || activeSnapshot.OffsetYAnimationType != m.OffsetY.AnimationType;
            var changedSpan = activeSnapshot.OffsetXSpan != m.OffsetX.Span || activeSnapshot.OffsetYSpan != m.OffsetY.Span;

            if (changedAnimType)
            {
                foreach (var p in selectedPins)
                {
                    p.OffsetX.AnimationType = m.OffsetX.AnimationType;
                    p.OffsetY.AnimationType = m.OffsetY.AnimationType;
                }
                return;
            }
            if (changedSpan)
            {
                foreach (var p in selectedPins)
                {
                    p.OffsetX.Span = m.OffsetX.Span;
                    p.OffsetY.Span = m.OffsetY.Span;
                }
                return;
            }

            var syncMode = Effect.SyncMode;
            if (syncMode == PuppetDeformationEditorPointsSync.None) return;

            var changedXIndex = FindChangedValueIndex(activeSnapshot.OffsetXValues, m.OffsetX);
            var changedYIndex = FindChangedValueIndex(activeSnapshot.OffsetYValues, m.OffsetY);
            if (changedXIndex < 0 && changedYIndex < 0) return;

            var sourceVector = new Vector2(
                (float)(m.RestX.Values.FirstOrDefault()?.Value ?? 0),
                (float)(m.RestY.Values.FirstOrDefault()?.Value ?? 0));

            var maxDistance = 1f;
            if (syncMode == PuppetDeformationEditorPointsSync.Distance)
            {
                var minX = selectedPins.Min(x => (float)(x.RestX.Values.FirstOrDefault()?.Value ?? 0));
                var maxX = selectedPins.Max(x => (float)(x.RestX.Values.FirstOrDefault()?.Value ?? 0));
                var minY = selectedPins.Min(x => (float)(x.RestY.Values.FirstOrDefault()?.Value ?? 0));
                var maxY = selectedPins.Max(x => (float)(x.RestY.Values.FirstOrDefault()?.Value ?? 0));
                Vector2[] corners = [new(minX, minY), new(maxX, minY), new(minX, maxY), new(maxX, maxY)];
                maxDistance = corners.Max(x => Vector2.Distance(x, sourceVector)) + 1f;
            }

            foreach (var point in selectedPins.Where(p => p != m))
            {
                var ratio = ComputeDistanceRatio(syncMode, point, sourceVector, maxDistance);
                ApplyValueDelta(changedXIndex, m.OffsetX, point.OffsetX, activeSnapshot.OffsetXValues, ratio);
                ApplyValueDelta(changedYIndex, m.OffsetY, point.OffsetY, activeSnapshot.OffsetYValues, ratio);
            }
        }

        static int FindChangedValueIndex(double[] oldValues, Animation animation)
        {
            for (var i = 0; i < Math.Min(oldValues.Length, animation.Values.Count); i++)
            {
                if (oldValues[i] != animation.Values[i].Value) return i;
            }
            return -1;
        }

        static float ComputeDistanceRatio(PuppetDeformationEditorPointsSync syncMode, PuppetDeformation point, Vector2 sourceVector, float maxDistance)
        {
            if (syncMode != PuppetDeformationEditorPointsSync.Distance) return 1f;
            var px = (float)(point.RestX.Values.FirstOrDefault()?.Value ?? 0);
            var py = (float)(point.RestY.Values.FirstOrDefault()?.Value ?? 0);
            var distance = Vector2.Distance(new Vector2(px, py), sourceVector);
            return Math.Max(0f, 1f - distance / maxDistance);
        }

        static void ApplyValueDelta(int changedIndex, Animation source, Animation target, double[] oldValues, float ratio)
        {
            if (changedIndex < 0) return;
            if (changedIndex >= source.Values.Count || changedIndex >= target.Values.Count || changedIndex >= oldValues.Length) return;
            var delta = source.Values[changedIndex].Value - oldValues[changedIndex];
            target.Values[changedIndex].Value += delta * ratio;
        }

        void OnBeginEditPoint()
        {
            //ピンの複数選択同期はピン編集時のみ必要。ボーン編集時はスナップショットを取らない
            if (EditMode == PuppetDeformationEditMode.Pin && selectedItem != null)
            {
                var m = selectedItem.Model;
                activeSnapshot = new EditSnapshot(m);
            }

            BeginEdit?.Invoke(this, EventArgs.Empty);
        }

        void OnEndEditPoint()
        {
            if (EditMode == PuppetDeformationEditMode.Pin && selectedItem != null)
            {
                SyncRestValues();
                SyncOffsetValues();
            }

            activeSnapshot = null;

            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        void Dispose(bool disposing)
        {
            if (disposedValue) return;
            if (disposing)
            {
                Effect.PropertyChanged -= Effect_PropertyChanged;
                foreach (var item in allViewModels)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.Dispose();
                }
                foreach (var bone in canvasBones)
                {
                    bone.PropertyChanged -= Bone_PropertyChanged;
                    bone.Dispose();
                }
            }
            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        sealed class EditSnapshot
        {
            public AnimationType RestXAnimationType { get; }
            public AnimationType RestYAnimationType { get; }
            public double RestXSpan { get; }
            public double RestYSpan { get; }
            public double[] RestXValues { get; }
            public double[] RestYValues { get; }

            public AnimationType OffsetXAnimationType { get; }
            public AnimationType OffsetYAnimationType { get; }
            public double OffsetXSpan { get; }
            public double OffsetYSpan { get; }
            public double[] OffsetXValues { get; }
            public double[] OffsetYValues { get; }

            public EditSnapshot(PuppetDeformation model)
            {
                RestXAnimationType = model.RestX.AnimationType;
                RestYAnimationType = model.RestY.AnimationType;
                RestXSpan = model.RestX.Span;
                RestYSpan = model.RestY.Span;
                RestXValues = model.RestX.Values.Select(x => x.Value).ToArray();
                RestYValues = model.RestY.Values.Select(x => x.Value).ToArray();

                OffsetXAnimationType = model.OffsetX.AnimationType;
                OffsetYAnimationType = model.OffsetY.AnimationType;
                OffsetXSpan = model.OffsetX.Span;
                OffsetYSpan = model.OffsetY.Span;
                OffsetXValues = model.OffsetX.Values.Select(x => x.Value).ToArray();
                OffsetYValues = model.OffsetY.Values.Select(x => x.Value).ToArray();
            }
        }
    }
}

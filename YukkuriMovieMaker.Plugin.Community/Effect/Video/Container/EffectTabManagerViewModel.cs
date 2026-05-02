using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class EffectTabManagerViewModel : Bindable, IDisposable
{
    private readonly ItemProperty[] _itemProperties;
    private readonly ContainerEffect _effect;
    private const string ClipboardFormat = "YukkuriMovieMaker.Plugin.Community.Effect.Video.Container.EffectTab";

    private bool _isSelfUpdating;

    public ObservableCollection<EffectTabItemViewModel> Tabs { get; } = new();

    private EffectTabItemViewModel? _selectedTab;
    public EffectTabItemViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab == value) return;

            if (_selectedTab != null)
                _selectedTab.SerializedEffects = EffectSerializer.Serialize(_effect.Effects);

            SelectTabInternal(value);

            if (_selectedTab != null)
                ApplyStateToEffectInternal(_selectedTab);
        }
    }

    public bool IsTabSelected => SelectedTab != null;

    public ActionCommand AddTabCommand { get; }
    public ActionCommand RemoveTabCommand { get; }
    public ActionCommand MoveTabLeftCommand { get; }
    public ActionCommand MoveTabRightCommand { get; }
    public ActionCommand DuplicateTabCommand { get; }
    public ActionCommand CopyCommand { get; }
    public ActionCommand PasteCommand { get; }
    public ActionCommand BeginEditCommand { get; }
    public ActionCommand CommitEditCommand { get; }
    public ActionCommand CancelEditCommand { get; }
    public ActionCommand TogglePinCommand { get; }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public EffectTabManagerViewModel() : this(Array.Empty<ItemProperty>()) { }

    public EffectTabManagerViewModel(ItemProperty[] itemProperties)
    {
        _itemProperties = itemProperties;
        _effect = itemProperties.Length > 0 ? (ContainerEffect)itemProperties[0].PropertyOwner : new ContainerEffect();

        AddTabCommand = new ActionCommand(_ => true, _ => ExecuteAddTab());
        RemoveTabCommand = new ActionCommand(p => Tabs.Count > 1 && (p as EffectTabItemViewModel ?? SelectedTab) != null && !(p as EffectTabItemViewModel ?? SelectedTab)!.IsPinned, p => ExecuteRemoveTab(p as EffectTabItemViewModel));
        MoveTabLeftCommand = new ActionCommand(p => CanMoveTab(p as EffectTabItemViewModel, -1), p => ExecuteMoveTab(p as EffectTabItemViewModel, -1));
        MoveTabRightCommand = new ActionCommand(p => CanMoveTab(p as EffectTabItemViewModel, 1), p => ExecuteMoveTab(p as EffectTabItemViewModel, 1));
        DuplicateTabCommand = new ActionCommand(p => (p as EffectTabItemViewModel ?? SelectedTab) != null, p => ExecuteDuplicateTab(p as EffectTabItemViewModel));
        CopyCommand = new ActionCommand(p => (p as EffectTabItemViewModel ?? SelectedTab) != null, p => ExecuteCopy(p as EffectTabItemViewModel));
        PasteCommand = new ActionCommand(_ => System.Windows.Clipboard.ContainsData(ClipboardFormat), p => ExecutePaste(p as EffectTabItemViewModel));
        BeginEditCommand = new ActionCommand(p => (p as EffectTabItemViewModel ?? SelectedTab) != null, p => ExecuteBeginEdit(p as EffectTabItemViewModel));
        CommitEditCommand = new ActionCommand(_ => true, p => ExecuteCommitEdit(p as EffectTabItemViewModel));
        CancelEditCommand = new ActionCommand(_ => true, p => ExecuteCancelEdit(p as EffectTabItemViewModel));
        TogglePinCommand = new ActionCommand(p => (p as EffectTabItemViewModel ?? SelectedTab) != null, p => ExecuteTogglePin(p as EffectTabItemViewModel));

        LoadTabs();
        _effect.PropertyChanged += OnEffectPropertyChanged;
    }

    private void LoadTabs()
    {
        var state = EffectTabStateService.ResolveEffectState(_effect.EffectTabsJson, _effect.Effects, Texts.EffectTab_FirstName);

        Tabs.Clear();

        var existingIds = state.Tabs.Select(t => t.Id).ToHashSet();
        foreach (var pinnedTab in EffectTabSettings.Default.PinnedTabs.Reverse())
        {
            if (!existingIds.Contains(pinnedTab.Id))
            {
                state.Tabs.Insert(0, new EffectTab
                {
                    Id = pinnedTab.Id,
                    Name = pinnedTab.Name,
                    SerializedEffects = pinnedTab.SerializedEffects
                });
                existingIds.Add(pinnedTab.Id);
            }
            else
            {
                var tab = state.Tabs.First(t => t.Id == pinnedTab.Id);
                state.Tabs.Remove(tab);
                state.Tabs.Insert(0, tab);
            }
        }

        foreach (var tab in state.Tabs)
            Tabs.Add(new EffectTabItemViewModel(tab));

        UpdateIndices();

        SelectedTab = state.SelectedTabId.HasValue
            ? Tabs.FirstOrDefault(t => t.Id == state.SelectedTabId.Value) ?? Tabs.FirstOrDefault()
            : Tabs.FirstOrDefault();
    }

    private void SelectTabInternal(EffectTabItemViewModel? tab)
    {
        _selectedTab = tab;
        foreach (var prop in _itemProperties)
            ((ContainerEffect)prop.PropertyOwner).SelectedTabName = tab?.Name;
        OnPropertyChanged(nameof(SelectedTab));
        OnPropertyChanged(nameof(IsTabSelected));
    }

    private void OnEffectPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isSelfUpdating) return;

        if (e.PropertyName == nameof(ContainerEffect.Effects) && SelectedTab != null)
        {
            using (BeginSelfUpdate())
            {
                SelectedTab.SerializedEffects = EffectSerializer.Serialize(_effect.Effects);
                PersistState();
            }
        }
        else if (e.PropertyName == nameof(ContainerEffect.EffectTabsJson))
        {
            using (BeginSelfUpdate())
            {
                LoadTabs();
            }
        }
    }

    private void ApplyStateToEffectInternal(EffectTabItemViewModel tab)
    {
        using (BeginSelfUpdate())
        {
            foreach (var prop in _itemProperties)
            {
                var target = (ContainerEffect)prop.PropertyOwner;
                target.Effects = EffectSerializer.Deserialize(tab.SerializedEffects);
                target.SelectedTabName = tab.Name;
            }
            PersistState();
        }
    }

    private void PersistState()
    {
        var state = new EffectTabState
        {
            SelectedTabId = SelectedTab?.Id,
            Tabs = Tabs.Select(t => t.Model).ToList()
        };

        var json = EffectTabStateService.Serialize(state);

        foreach (var prop in _itemProperties)
        {
            var target = (ContainerEffect)prop.PropertyOwner;
            if (target.EffectTabsJson != json)
                target.EffectTabsJson = json;
        }
    }

    public void MoveTab(int sourceIndex, int targetIndex)
    {
        if (sourceIndex == targetIndex) return;

        var sourceTab = Tabs[sourceIndex];
        Tabs.Move(sourceIndex, targetIndex);

        if (sourceTab.IsPinned)
        {
            var pinnedTabs = Tabs.Where(t => t.IsPinned).Select(t => t.Model.Id).ToList();
            var newOrder = new System.Collections.ObjectModel.ObservableCollection<EffectTab>();
            foreach (var id in pinnedTabs)
            {
                var matching = EffectTabSettings.Default.PinnedTabs.FirstOrDefault(p => p.Id == id);
                if (matching != null)
                    newOrder.Add(matching);
            }

            EffectTabSettings.Default.PinnedTabs.Clear();
            foreach (var matching in newOrder)
            {
                EffectTabSettings.Default.PinnedTabs.Add(matching);
            }
            EffectTabSettings.Default.Save();
        }

        UpdateIndices();
        PersistState();
    }

    private void UpdateIndices()
    {
        for (int i = 0; i < Tabs.Count; i++)
            Tabs[i].Index = i;
    }

    private void ExecuteAddTab()
    {
        using (BeginUndo())
        {
            var tab = new EffectTab { Name = string.Format(Texts.EffectTab_NumberedName, Tabs.Count + 1), SerializedEffects = string.Empty };
            var vm = new EffectTabItemViewModel(tab);
            Tabs.Add(vm);
            UpdateIndices();
            SelectedTab = vm;
        }
    }

    private void ExecuteRemoveTab(EffectTabItemViewModel? tabVm)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null || Tabs.Count <= 1 || target.IsPinned) return;

        using (BeginUndo())
        {
            var wasSelected = SelectedTab == target;
            Tabs.Remove(target);
            UpdateIndices();

            if (wasSelected)
            {
                var next = Tabs[0];
                SelectTabInternal(next);
                ApplyStateToEffectInternal(next);
            }
            else
            {
                PersistState();
            }
        }
    }

    private bool CanMoveTab(EffectTabItemViewModel? tabVm, int offset)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null) return false;
        var idx = Tabs.IndexOf(target);
        if (idx < 0) return false;
        var newIdx = idx + offset;

        if (newIdx < 0 || newIdx >= Tabs.Count) return false;

        var pinnedCount = Tabs.Count(t => t.IsPinned);
        if (target.IsPinned && newIdx >= pinnedCount) return false;
        if (!target.IsPinned && newIdx < pinnedCount) return false;

        return true;
    }

    private void ExecuteMoveTab(EffectTabItemViewModel? tabVm, int offset)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null) return;
        var idx = Tabs.IndexOf(target);
        var newIdx = idx + offset;
        if (newIdx >= 0 && newIdx < Tabs.Count)
        {
            using (BeginUndo())
            {
                Tabs.Move(idx, newIdx);
                UpdateIndices();
                PersistState();
            }
        }
    }

    private void ExecuteDuplicateTab(EffectTabItemViewModel? tabVm)
    {
        var source = tabVm ?? SelectedTab;
        if (source == null) return;

        using (BeginUndo())
        {
            if (source == SelectedTab)
                source.SerializedEffects = EffectSerializer.Serialize(_effect.Effects);

            var dup = new EffectTab
            {
                Name = source.Name + Texts.EffectTab_CopyName,
                SerializedEffects = source.SerializedEffects
            };
            var vm = new EffectTabItemViewModel(dup);
            var idx = Tabs.IndexOf(source);
            Tabs.Insert(idx + 1, vm);
            UpdateIndices();
            SelectedTab = vm;
        }
    }

    private void ExecuteCopy(EffectTabItemViewModel? tabVm)
    {
        var source = tabVm ?? SelectedTab;
        if (source == null) return;

        if (source == SelectedTab)
            source.SerializedEffects = EffectSerializer.Serialize(_effect.Effects);

        var data = new EffectTab
        {
            Name = source.Name,
            SerializedEffects = source.SerializedEffects
        };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        System.Windows.Clipboard.SetData(ClipboardFormat, json);
    }

    private void ExecutePaste(EffectTabItemViewModel? targetTabVm)
    {
        if (!System.Windows.Clipboard.ContainsData(ClipboardFormat)) return;

        var raw = System.Windows.Clipboard.GetData(ClipboardFormat) as string;
        if (string.IsNullOrWhiteSpace(raw)) return;

        EffectTab? data;
        try
        {
            data = Newtonsoft.Json.JsonConvert.DeserializeObject<EffectTab>(raw);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return;
        }

        if (data == null) return;

        using (BeginUndo())
        {
            if (targetTabVm != null)
            {
                targetTabVm.SerializedEffects = data.SerializedEffects;
                if (targetTabVm == SelectedTab)
                    ApplyStateToEffectInternal(targetTabVm);
                else
                    PersistState();
            }
            else
            {
                var tab = new EffectTab
                {
                    Name = data.Name + Texts.EffectTab_CopyName,
                    SerializedEffects = data.SerializedEffects
                };
                var vm = new EffectTabItemViewModel(tab);
                Tabs.Add(vm);
                UpdateIndices();
                SelectedTab = vm;
            }
        }
    }

    private void ExecuteTogglePin(EffectTabItemViewModel? tabVm)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null) return;
        using (BeginUndo())
        {
            var oldPinned = target.IsPinned;
            target.IsPinned = !target.IsPinned;

            if (target.IsPinned)
            {
                var idx = Tabs.IndexOf(target);
                if (idx > 0)
                {
                    Tabs.Move(idx, 0);
                    UpdateIndices();
                    PersistState();
                }
            }

            RemoveTabCommand.RaiseCanExecuteChanged();
            MoveTabLeftCommand.RaiseCanExecuteChanged();
            MoveTabRightCommand.RaiseCanExecuteChanged();
        }
    }

    private void ExecuteBeginEdit(EffectTabItemViewModel? tabVm)
    {
        (tabVm ?? SelectedTab)?.BeginEdit();
    }

    private void ExecuteCommitEdit(EffectTabItemViewModel? tabVm)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null) return;

        using (BeginUndo())
        {
            target.CommitEdit(Texts.EffectTab_FirstName);

            if (target == SelectedTab)
            {
                foreach (var prop in _itemProperties)
                    ((ContainerEffect)prop.PropertyOwner).SelectedTabName = target.Name;
            }

            PersistState();
        }
    }

    private void ExecuteCancelEdit(EffectTabItemViewModel? tabVm)
    {
        (tabVm ?? SelectedTab)?.CancelEdit();
    }

    public void MoveTab(EffectTabItemViewModel source, EffectTabItemViewModel target)
    {
        var srcIdx = Tabs.IndexOf(source);
        var dstIdx = Tabs.IndexOf(target);
        if (srcIdx < 0 || dstIdx < 0 || srcIdx == dstIdx) return;

        using (BeginUndo())
        {
            Tabs.Move(srcIdx, dstIdx);
            UpdateIndices();
            PersistState();
        }
    }

    private IDisposable BeginUndo() => new StateScope(this, true);
    private IDisposable BeginSelfUpdate() => new StateScope(this, false);

    private sealed class StateScope : IDisposable
    {
        private readonly EffectTabManagerViewModel _vm;
        private readonly bool _useUndo;
        private readonly bool _wasSelfUpdating;

        public StateScope(EffectTabManagerViewModel vm, bool useUndo)
        {
            _vm = vm;
            _useUndo = useUndo;
            _wasSelfUpdating = _vm._isSelfUpdating;

            _vm._isSelfUpdating = true;
            if (_useUndo)
                _vm.BeginEdit?.Invoke(_vm, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_useUndo)
                _vm.EndEdit?.Invoke(_vm, EventArgs.Empty);

            _vm._isSelfUpdating = _wasSelfUpdating;
        }
    }

    public void Dispose()
    {
        _effect.PropertyChanged -= OnEffectPropertyChanged;
    }
}

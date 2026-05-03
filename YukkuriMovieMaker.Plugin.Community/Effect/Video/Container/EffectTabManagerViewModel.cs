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
    public bool HasMultipleTabs => Tabs.Count > 1;

    public ActionCommand AddTabCommand { get; }
    public ActionCommand RemoveTabCommand { get; }
    public ActionCommand MoveTabLeftCommand { get; }
    public ActionCommand MoveTabRightCommand { get; }
    public ActionCommand DuplicateTabCommand { get; }
    public ActionCommand CopyCommand { get; }
    public ActionCommand PasteCommand { get; }
    public ActionCommand StashCommand { get; }
    public ActionCommand RestoreStashCommand { get; }
    public ActionCommand RemoveStashCommand { get; }
    public ActionCommand BeginEditCommand { get; }
    public ActionCommand CommitEditCommand { get; }
    public ActionCommand CancelEditCommand { get; }
    public ActionCommand MoveTabToIndexCommand { get; }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public ObservableCollection<EffectTabStashViewModel> Stashes { get; } = new();
    public bool HasStashes => Stashes.Count > 0;

    public EffectTabManagerViewModel(ItemProperty[] itemProperties)
    {
        _itemProperties = itemProperties;
        _effect = itemProperties.Length > 0 ? (ContainerEffect)itemProperties[0].PropertyOwner : new ContainerEffect();

        Tabs.CollectionChanged += Tabs_CollectionChanged;

        AddTabCommand = new ActionCommand(_ => true, _ => ExecuteAddTab());
        RemoveTabCommand = new ActionCommand(p => ResolveTab(p) != null, p => ExecuteRemoveTab(ResolveTab(p)));
        MoveTabLeftCommand = new ActionCommand(p => CanMoveTab(ResolveTab(p), -1), p => ExecuteMoveTab(ResolveTab(p), -1));
        MoveTabRightCommand = new ActionCommand(p => CanMoveTab(ResolveTab(p), 1), p => ExecuteMoveTab(ResolveTab(p), 1));
        DuplicateTabCommand = new ActionCommand(p => ResolveTab(p) != null, p => ExecuteDuplicateTab(ResolveTab(p)));
        CopyCommand = new ActionCommand(p => ResolveTab(p) != null, p => ExecuteCopy(ResolveTab(p)));
        PasteCommand = new ActionCommand(_ => System.Windows.Clipboard.ContainsData(ClipboardFormat), p => ExecutePaste(p as EffectTabItemViewModel));
        StashCommand = new ActionCommand(p => ResolveTab(p) != null, p => ExecuteStash(ResolveTab(p)));
        RestoreStashCommand = new ActionCommand(p => p is EffectTabStashViewModel, p => ExecuteRestoreStash(p as EffectTabStashViewModel));
        RemoveStashCommand = new ActionCommand(p => p is EffectTabStashViewModel, p => ExecuteRemoveStash(p as EffectTabStashViewModel));
        BeginEditCommand = new ActionCommand(p => ResolveTab(p) != null, p => ExecuteBeginEdit(ResolveTab(p)));
        CommitEditCommand = new ActionCommand(_ => true, p => ExecuteCommitEdit(p as EffectTabItemViewModel));
        CancelEditCommand = new ActionCommand(_ => true, p => ExecuteCancelEdit(p as EffectTabItemViewModel));
        MoveTabToIndexCommand = new ActionCommand(
            p => p is MoveTabToIndexParameter param && CanMoveTabToIndex(param),
            p => ExecuteMoveTabToIndex(p as MoveTabToIndexParameter));

        LoadTabs();
        LoadStashes();

        _effect.PropertyChanged += OnEffectPropertyChanged;
    }

    private EffectTabItemViewModel? ResolveTab(object? param) => param as EffectTabItemViewModel ?? SelectedTab;

    private void Tabs_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasMultipleTabs));
    }

    private void ForEachEffect(Action<ContainerEffect> action)
    {
        foreach (var prop in _itemProperties)
            action((ContainerEffect)prop.PropertyOwner);
    }

    private void LoadStashes()
    {
        EffectTabStashSettings.Default.Stashes.CollectionChanged += OnStashesChanged;
        SyncStashes();
    }

    private void SyncStashes()
    {
        Stashes.Clear();
        foreach (var stash in EffectTabStashSettings.Default.Stashes)
            Stashes.Add(new EffectTabStashViewModel(stash));
        OnPropertyChanged(nameof(HasStashes));
    }

    private void OnStashesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => SyncStashes();

    private void LoadTabs()
    {
        var state = EffectTabStateService.ResolveEffectState(_effect.EffectTabsJson, _effect.Effects, Texts.EffectTab_FirstName);

        Tabs.Clear();
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
        ForEachEffect(e => e.SelectedTabName = tab?.Name);
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
            ForEachEffect(e =>
            {
                e.Effects = EffectSerializer.Deserialize(tab.SerializedEffects);
                e.SelectedTabName = tab.Name;
            });
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
        ForEachEffect(e =>
        {
            if (e.EffectTabsJson != json)
                e.EffectTabsJson = json;
        });
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

    private void ExecuteRemoveTab(EffectTabItemViewModel? target)
    {
        if (target == null || !HasMultipleTabs) return;

        using (BeginUndo())
        {
            RemoveTabInternal(target);
        }
    }

    private void ExecuteStash(EffectTabItemViewModel? target)
    {
        if (target == null) return;

        if (target == SelectedTab)
            target.SerializedEffects = EffectSerializer.Serialize(_effect.Effects);

        var effects = EffectSerializer.Deserialize(target.SerializedEffects);
        if (effects.Count == 0) return;

        var firstEffectName = effects[0].Label;
        var name = effects.Count > 1
            ? string.Format(Texts.Menu_StashNameFormat, target.Name, firstEffectName, effects.Count - 1)
            : string.Format(Texts.Menu_StashNameFormatSingle, target.Name, firstEffectName);

        var stash = new EffectTab
        {
            Id = Guid.NewGuid(),
            Name = name,
            SerializedEffects = target.SerializedEffects
        };

        EffectTabStashSettings.Default.Stashes.Add(stash);
        EffectTabStashSettings.Default.Save();

        using (BeginUndo())
        {
            RemoveTabInternal(target);
        }
    }

    private void RemoveTabInternal(EffectTabItemViewModel target)
    {
        var wasSelected = SelectedTab == target;
        Tabs.Remove(target);

        if (Tabs.Count == 0)
        {
            var tab = new EffectTab { Name = Texts.EffectTab_FirstName, SerializedEffects = string.Empty };
            Tabs.Add(new EffectTabItemViewModel(tab));
        }

        UpdateIndices();

        if (wasSelected)
        {
            var next = Tabs.First();
            SelectTabInternal(next);
            ApplyStateToEffectInternal(next);
        }
        else
        {
            PersistState();
        }
    }

    private bool CanMoveTab(EffectTabItemViewModel? target, int offset)
    {
        if (target == null) return false;
        var idx = Tabs.IndexOf(target);
        if (idx < 0) return false;
        var newIdx = idx + offset;
        return newIdx >= 0 && newIdx < Tabs.Count;
    }

    private void ExecuteMoveTab(EffectTabItemViewModel? target, int offset)
    {
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

    private bool CanMoveTabToIndex(MoveTabToIndexParameter param)
    {
        var sourceIndex = Tabs.IndexOf(param.Tab);
        if (sourceIndex < 0) return false;
        var target = param.TargetIndex;

        return target >= 0 && target <= Tabs.Count
            && target != sourceIndex
            && target != sourceIndex + 1;
    }

    private void ExecuteMoveTabToIndex(MoveTabToIndexParameter? param)
    {
        if (param == null) return;
        var sourceIndex = Tabs.IndexOf(param.Tab);
        if (sourceIndex < 0) return;

        var adjustedIndex = param.TargetIndex > sourceIndex
            ? param.TargetIndex - 1
            : param.TargetIndex;

        if (adjustedIndex == sourceIndex) return;
        if (adjustedIndex < 0 || adjustedIndex >= Tabs.Count) return;

        using (BeginUndo())
        {
            Tabs.Move(sourceIndex, adjustedIndex);
            UpdateIndices();
            PersistState();
        }
    }

    private void ExecuteDuplicateTab(EffectTabItemViewModel? source)
    {
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

    private void ExecuteCopy(EffectTabItemViewModel? source)
    {
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

    private void ExecuteRestoreStash(EffectTabStashViewModel? stashVm)
    {
        if (stashVm == null) return;

        using (BeginUndo())
        {
            var tab = new EffectTab
            {
                Name = Texts.Menu_RestoreStashName,
                SerializedEffects = stashVm.SerializedEffects
            };
            var vm = new EffectTabItemViewModel(tab);
            Tabs.Add(vm);
            UpdateIndices();
            SelectedTab = vm;
        }

        EffectTabStashSettings.Default.Stashes.Remove(stashVm.Model);
        EffectTabStashSettings.Default.Save();
    }

    private void ExecuteRemoveStash(EffectTabStashViewModel? stashVm)
    {
        if (stashVm == null) return;
        EffectTabStashSettings.Default.Stashes.Remove(stashVm.Model);
        EffectTabStashSettings.Default.Save();
    }

    private void ExecuteBeginEdit(EffectTabItemViewModel? target)
    {
        target?.BeginEdit();
    }

    private void ExecuteCommitEdit(EffectTabItemViewModel? target)
    {
        if (target == null) return;

        using (BeginUndo())
        {
            target.CommitEdit(Texts.EffectTab_FirstName);
            if (target == SelectedTab)
                ForEachEffect(e => e.SelectedTabName = target.Name);

            PersistState();
        }
    }

    private void ExecuteCancelEdit(EffectTabItemViewModel? target)
    {
        target?.CancelEdit();
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
        Tabs.CollectionChanged -= Tabs_CollectionChanged;
        EffectTabStashSettings.Default.Stashes.CollectionChanged -= OnStashesChanged;
        _effect.PropertyChanged -= OnEffectPropertyChanged;
    }
}

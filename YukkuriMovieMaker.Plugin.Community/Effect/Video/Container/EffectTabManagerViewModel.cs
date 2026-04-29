using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class EffectTabManagerViewModel : Bindable, IDisposable
{
    private readonly ItemProperty[] _itemProperties;
    private readonly ContainerEffect _effect;

    public ObservableCollection<EffectTabItemViewModel> Tabs { get; } = new();

    private EffectTabItemViewModel? _selectedTab;
    public EffectTabItemViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (Set(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsTabSelected));
                if (_selectedTab != null)
                {
                    ApplyTabToEffect(_selectedTab);
                }
            }
        }
    }

    public bool IsTabSelected => SelectedTab != null;

    private bool _isCompactMode;
    public bool IsCompactMode
    {
        get => _isCompactMode;
        set => Set(ref _isCompactMode, value);
    }

    public ICommand AddTabCommand { get; }
    public ICommand RemoveTabCommand { get; }
    public ICommand MoveTabLeftCommand { get; }
    public ICommand MoveTabRightCommand { get; }
    public ICommand DuplicateTabCommand { get; }
    public ICommand BeginEditCommand { get; }
    public ICommand CommitEditCommand { get; }
    public ICommand CancelEditCommand { get; }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;
    public event Action<Guid>? RenameRequested;

    public EffectTabManagerViewModel() : this(Array.Empty<ItemProperty>()) { }

    public EffectTabManagerViewModel(ItemProperty[] itemProperties)
    {
        _itemProperties = itemProperties;
        _effect = itemProperties.Length > 0 ? (ContainerEffect)itemProperties[0].PropertyOwner : new ContainerEffect();

        AddTabCommand = new ActionCommand(_ => true, _ => ExecuteAddTab());
        RemoveTabCommand = new ActionCommand(p => Tabs.Count > 1 && (p as EffectTabItemViewModel ?? SelectedTab) != null, p => ExecuteRemoveTab(p as EffectTabItemViewModel));
        MoveTabLeftCommand = new ActionCommand(p => CanMoveTab(p as EffectTabItemViewModel, -1), p => ExecuteMoveTab(p as EffectTabItemViewModel, -1));
        MoveTabRightCommand = new ActionCommand(p => CanMoveTab(p as EffectTabItemViewModel, 1), p => ExecuteMoveTab(p as EffectTabItemViewModel, 1));
        DuplicateTabCommand = new ActionCommand(p => (p as EffectTabItemViewModel ?? SelectedTab) != null, p => ExecuteDuplicateTab(p as EffectTabItemViewModel));
        BeginEditCommand = new ActionCommand(p => (p as EffectTabItemViewModel ?? SelectedTab) != null, p => ExecuteBeginEdit(p as EffectTabItemViewModel));
        CommitEditCommand = new ActionCommand(_ => true, p => ExecuteCommitEdit(p as EffectTabItemViewModel));
        CancelEditCommand = new ActionCommand(_ => true, p => ExecuteCancelEdit(p as EffectTabItemViewModel));

        LoadTabs();
        _effect.PropertyChanged += OnEffectPropertyChanged;
    }

    private void LoadTabs()
    {
        var state = EffectTabStateService.ResolveEffectState(_effect.EffectTabsJson, _effect.Effects, Texts.EffectTab_FirstName);

        Tabs.Clear();
        foreach (var tab in state.Tabs)
        {
            Tabs.Add(new EffectTabItemViewModel(tab));
        }

        UpdateIndices();

        if (state.SelectedTabId != null)
        {
            SelectedTab = Tabs.FirstOrDefault(t => t.Id == state.SelectedTabId.Value);
        }
        if (SelectedTab == null)
        {
            SelectedTab = Tabs.FirstOrDefault();
        }
    }

    private void OnEffectPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContainerEffect.Effects) && SelectedTab != null)
        {
            SelectedTab.SerializedEffects = EffectSerializer.Serialize(_effect.Effects);
            WriteState();
        }
    }

    private void ApplyTabToEffect(EffectTabItemViewModel tab)
    {
        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            foreach (var prop in _itemProperties)
            {
                var target = (ContainerEffect)prop.PropertyOwner;
                target.Effects = EffectSerializer.Deserialize(tab.SerializedEffects);
            }
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
        WriteState();
    }

    private void WriteState()
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
            target.EffectTabsJson = json;
        }
    }

    private void SaveState()
    {
        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            WriteState();
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateIndices()
    {
        for (int i = 0; i < Tabs.Count; i++)
        {
            Tabs[i].Index = i;
        }
    }

    private void ExecuteAddTab()
    {
        var tab = new EffectTab { Name = string.Format(Texts.EffectTab_NumberedName, Tabs.Count + 1), SerializedEffects = string.Empty };
        var vm = new EffectTabItemViewModel(tab);
        Tabs.Add(vm);
        UpdateIndices();
        SelectedTab = vm;
    }

    private void ExecuteRemoveTab(EffectTabItemViewModel? tabVm)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null || Tabs.Count <= 1) return;

        Tabs.Remove(target);
        UpdateIndices();
        if (SelectedTab == target || SelectedTab == null)
            SelectedTab = Tabs.FirstOrDefault();
        else
            SaveState();
    }

    private bool CanMoveTab(EffectTabItemViewModel? tabVm, int offset)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null) return false;
        var idx = Tabs.IndexOf(target);
        if (idx < 0) return false;
        var newIdx = idx + offset;
        return newIdx >= 0 && newIdx < Tabs.Count;
    }

    private void ExecuteMoveTab(EffectTabItemViewModel? tabVm, int offset)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null) return;
        var idx = Tabs.IndexOf(target);
        var newIdx = idx + offset;
        if (newIdx >= 0 && newIdx < Tabs.Count)
        {
            Tabs.Move(idx, newIdx);
            UpdateIndices();
            SaveState();
        }
    }

    private void ExecuteDuplicateTab(EffectTabItemViewModel? tabVm)
    {
        var target = tabVm ?? SelectedTab;
        if (target == null) return;

        var dup = new EffectTab
        {
            Name = target.Name + Texts.EffectTab_CopyName,
            SerializedEffects = target.SerializedEffects
        };
        var vm = new EffectTabItemViewModel(dup);
        var idx = Tabs.IndexOf(target);
        Tabs.Insert(idx + 1, vm);
        UpdateIndices();
        SelectedTab = vm;
    }

    private void ExecuteBeginEdit(EffectTabItemViewModel? tabVm)
    {
        var target = tabVm ?? SelectedTab;
        if (target != null)
        {
            target.BeginEdit();
            RenameRequested?.Invoke(target.Id);
        }
    }

    private void ExecuteCommitEdit(EffectTabItemViewModel? tabVm)
    {
        var target = tabVm ?? SelectedTab;
        if (target != null)
        {
            target.CommitEdit(Texts.EffectTab_FirstName);
            SaveState();
        }
    }

    private void ExecuteCancelEdit(EffectTabItemViewModel? tabVm)
    {
        var target = tabVm ?? SelectedTab;
        target?.CancelEdit();
    }

    public void MoveTab(EffectTabItemViewModel source, EffectTabItemViewModel target)
    {
        var srcIdx = Tabs.IndexOf(source);
        var dstIdx = Tabs.IndexOf(target);
        if (srcIdx >= 0 && dstIdx >= 0 && srcIdx != dstIdx)
        {
            Tabs.Move(srcIdx, dstIdx);
            UpdateIndices();
            SaveState();
        }
    }

    public void Dispose()
    {
        _effect.PropertyChanged -= OnEffectPropertyChanged;
    }
}
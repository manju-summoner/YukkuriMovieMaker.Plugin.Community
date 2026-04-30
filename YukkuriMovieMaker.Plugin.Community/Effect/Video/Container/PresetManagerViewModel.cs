using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class PresetManagerViewModel : Bindable, IDisposable
{
    private const int MaxRecentPresets = 20;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;

    private static readonly JsonSerializerSettings ExchangeSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    private readonly ItemProperty[] _itemProperties;
    private readonly ContainerEffect _effect;

    public ObservableCollection<PresetGroup> Groups { get; } = new();
    public ObservableCollection<PresetItemViewModel> DisplayedPresets { get; } = new();

    private PresetGroup? _selectedGroup;
    public PresetGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (Set(ref _selectedGroup, value))
            {
                RefreshDisplayedPresets();
                OnPropertyChanged(nameof(IsCurrentGroupVirtual));
                UpdateActionCommands();
            }
        }
    }

    private PresetSearchMode _searchMode = PresetSearchMode.Name;
    public PresetSearchMode SearchMode
    {
        get => _searchMode;
        set
        {
            if (Set(ref _searchMode, value))
            {
                OnPropertyChanged(nameof(IsSearchModeName));
                OnPropertyChanged(nameof(IsSearchModeEffectName));
                OnPropertyChanged(nameof(IsSearchModeEffectCount));
                OnPropertyChanged(nameof(IsSearchModeRawJson));
                OnPropertyChanged(nameof(IsSearchModeAny));
                RefreshDisplayedPresets();
            }
        }
    }

    public bool IsSearchModeName => SearchMode == PresetSearchMode.Name;
    public bool IsSearchModeEffectName => SearchMode == PresetSearchMode.EffectName;
    public bool IsSearchModeEffectCount => SearchMode == PresetSearchMode.EffectCount;
    public bool IsSearchModeRawJson => SearchMode == PresetSearchMode.RawJson;
    public bool IsSearchModeAny => SearchMode == PresetSearchMode.Any;

    private PresetItemViewModel? _selectedPreset;
    public PresetItemViewModel? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (Set(ref _selectedPreset, value))
                UpdateActionCommands();
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
                RefreshDisplayedPresets();
        }
    }

    public bool IsCurrentGroupVirtual => SelectedGroup?.IsVirtual == true;

    public ActionCommand AddGroupCommand { get; }
    public ActionCommand RemoveGroupCommand { get; }
    public ActionCommand RenameGroupCommand { get; }
    public ActionCommand AddPresetCommand { get; }
    public ActionCommand RemovePresetCommand { get; }
    public ActionCommand UpdatePresetCommand { get; }
    public ActionCommand ApplyPresetCommand { get; }
    public ActionCommand RenamePresetCommand { get; }
    public ActionCommand ToggleFavoriteCommand { get; }
    public ActionCommand ClearUnselectedCommand { get; }
    public ActionCommand ApplySinglePresetCommand { get; }
    public ActionCommand ClearPresetCommand { get; }
    public ActionCommand SetSearchModeCommand { get; }
    public ActionCommand ExportPresetsCommand { get; }
    public ActionCommand ImportPresetsCommand { get; }
    public ActionCommand CopyPresetCommand { get; }
    public ActionCommand PastePresetCommand { get; }
    public ActionCommand CutPresetCommand { get; }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;
    public event Action<PresetGroup>? GroupRenameRequested;
    public event Action<PresetItemViewModel>? PresetRenameRequested;

    private Guid? _appliedPresetId;
    private bool _canUpdatePresetCache;
    private ImmutableList<IVideoEffect> _trackedEffects = ImmutableList<IVideoEffect>.Empty;
    private bool _disposed;
    private bool _hasPotentialUnnotifiedEffectMutation;
    private DispatcherOperation? _pendingInputDrivenCheckOperation;

    public PresetManagerViewModel() : this(Array.Empty<ItemProperty>()) { }

    public PresetManagerViewModel(ItemProperty[] itemProperties)
    {
        _itemProperties = itemProperties;
        _effect = itemProperties.Length > 0 ? (ContainerEffect)itemProperties[0].PropertyOwner : new ContainerEffect();

        AddGroupCommand = new ActionCommand(_ => true, _ => ExecuteAddGroup());
        RemoveGroupCommand = new ActionCommand(_ => CanRemoveGroup(), _ => ExecuteRemoveGroup());
        RenameGroupCommand = new ActionCommand(p => CanRenameGroup(p as PresetGroup), p => ExecuteRenameGroup(p as PresetGroup));
        AddPresetCommand = new ActionCommand(_ => true, _ => ExecuteAddPreset());
        RemovePresetCommand = new ActionCommand(p => ResolveTargets(p).Count > 0, ExecuteRemovePreset);
        UpdatePresetCommand = new ActionCommand(CanUpdatePreset, ExecuteUpdatePreset);
        ApplyPresetCommand = new ActionCommand(p => ResolveTargets(p).Count > 0, ExecuteApplyPreset);
        RenamePresetCommand = new ActionCommand(p => ResolveTargets(p).Count == 1, ExecuteRenamePreset);
        ToggleFavoriteCommand = new ActionCommand(p => ResolveTargets(p).Count > 0, ExecuteToggleFavorite);
        ClearUnselectedCommand = new ActionCommand(_ => true, _ => ExecuteClearUnselected());
        ApplySinglePresetCommand = new ActionCommand(p => ResolveTargets(p).Count == 1, ExecuteApplySinglePreset);
        ClearPresetCommand = new ActionCommand(p => ResolveTargets(p).Count == 1, ExecuteClearPreset);
        SetSearchModeCommand = new ActionCommand(_ => true, ExecuteSetSearchMode);
        ExportPresetsCommand = new ActionCommand(p => ResolveTargets(p).Count > 0, ExecuteExportPresets);
        ImportPresetsCommand = new ActionCommand(_ => true, _ => ExecuteImportPresets());
        CopyPresetCommand = new ActionCommand(p => ResolveTargets(p).Count > 0, ExecuteCopyPreset);
        PastePresetCommand = new ActionCommand(_ => true, _ => ExecutePastePreset());
        CutPresetCommand = new ActionCommand(p => ResolveTargets(p).Count > 0, ExecuteCutPreset);

        _effect.PropertyChanged += OnEffectPropertyChanged;
        InputManager.Current.PostProcessInput += OnPostProcessInput;
        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
        UpdateAppliedPresetId();
        AttachEffectHandlers(_effect.Effects);
        TriggerUpdateCheck();

        LoadData();
    }

    public static bool IsVirtualGroup(PresetGroup? group) => group?.IsVirtual == true;

    private bool CanUpdatePreset(object? parameter)
    {
        var targets = ResolveTargets(parameter);
        if (targets.Count != 1) return false;
        if (!_canUpdatePresetCache || _appliedPresetId == null) return false;
        return targets[0].Model.Id == _appliedPresetId;
    }

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContainerEffect.SelectedPresetJson))
        {
            UpdateAppliedPresetId();
            TriggerUpdateCheck();
        }
        else if (e.PropertyName == nameof(ContainerEffect.Effects))
        {
            AttachEffectHandlers(_effect.Effects);
            TriggerUpdateCheck();
        }
        else if (e.PropertyName == nameof(ContainerEffect.EffectTabsJson))
        {
            TriggerUpdateCheck();
        }
    }

    private void AttachEffectHandlers(ImmutableList<IVideoEffect> effects)
    {
        foreach (var effect in _trackedEffects)
        {
            if (effect is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= OnVideoEffectPropertyChanged;
        }

        _trackedEffects = effects;

        foreach (var effect in _trackedEffects)
        {
            if (effect is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += OnVideoEffectPropertyChanged;
        }
    }

    private void OnVideoEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        TriggerUpdateCheck();
    }

    private void UpdateAppliedPresetId()
    {
        if (string.IsNullOrEmpty(_effect.SelectedPresetJson))
        {
            _appliedPresetId = null;
            return;
        }
        try
        {
            var preset = JsonConvert.DeserializeObject<EffectPreset>(_effect.SelectedPresetJson);
            _appliedPresetId = preset?.Id;
        }
        catch
        {
            _appliedPresetId = null;
        }
    }

    private void TriggerUpdateCheck()
    {
        if (_appliedPresetId == null)
        {
            SetUpdateCache(false);
            return;
        }

        try
        {
            var preset = ContainerSettings.Instance.Presets.FirstOrDefault(p => p.Id == _appliedPresetId);
            if (preset == null)
            {
                SetUpdateCache(false);
                return;
            }

            var rawCurrentState = EffectTabStateService.ResolveEffectState(_effect.EffectTabsJson, _effect.Effects, Texts.EffectTab_FirstName);
            var currentState = EffectTabStateService.DeepCopy(rawCurrentState);
            var currentSelectedTab = EffectTabStateService.GetSelectedTab(currentState);
            currentSelectedTab.SerializedEffects = EffectSerializer.Serialize(_effect.Effects);
            foreach (var tab in currentState.Tabs) tab.Id = Guid.Empty;
            currentState.SelectedTabId = Guid.Empty;
            var currentStateJson = EffectTabStateService.Serialize(currentState);

            var rawPresetState = EffectTabStateService.ResolvePresetState(preset, Texts.EffectTab_FirstName);
            var presetState = EffectTabStateService.DeepCopy(rawPresetState);
            var presetSelectedTab = EffectTabStateService.GetSelectedTab(presetState);
            var presetEffects = EffectSerializer.Deserialize(presetSelectedTab.SerializedEffects);
            presetSelectedTab.SerializedEffects = EffectSerializer.Serialize(presetEffects);
            foreach (var tab in presetState.Tabs) tab.Id = Guid.Empty;
            presetState.SelectedTabId = Guid.Empty;
            var presetStateJson = EffectTabStateService.Serialize(presetState);

            var isDirty = !string.Equals(currentStateJson, presetStateJson, StringComparison.Ordinal);
            SetUpdateCache(isDirty);
        }
        catch
        {
            SetUpdateCache(false);
        }
    }

    private void SetUpdateCache(bool value)
    {
        if (_canUpdatePresetCache == value) return;
        _canUpdatePresetCache = value;
        UpdateActionCommands();
    }

    private void OnPostProcessInput(object? sender, ProcessInputEventArgs e)
    {
        if (_disposed) return;

        var input = e.StagingItem.Input;
        if (input is MouseButtonEventArgs mouseButtonEvent &&
            mouseButtonEvent.ChangedButton == MouseButton.Left &&
            mouseButtonEvent.ButtonState == MouseButtonState.Pressed)
        {
            _hasPotentialUnnotifiedEffectMutation = true;
            return;
        }

        if (input is MouseEventArgs mouseMove)
        {
            if (mouseMove.LeftButton == MouseButtonState.Pressed)
                _hasPotentialUnnotifiedEffectMutation = true;
            return;
        }

        if (input is not MouseButtonEventArgs mouseButton) return;
        if (mouseButton.ChangedButton != MouseButton.Left || mouseButton.ButtonState != MouseButtonState.Released) return;

        QueueInputDrivenUpdateCheck();
    }

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (_disposed) return;

        switch ((int)msg.message)
        {
            case WmLeftButtonDown:
                _hasPotentialUnnotifiedEffectMutation = true;
                break;
            case WmLeftButtonUp:
                QueueInputDrivenUpdateCheck();
                break;
        }
    }

    private void QueueInputDrivenUpdateCheck()
    {
        if (!_hasPotentialUnnotifiedEffectMutation) return;

        if (_pendingInputDrivenCheckOperation != null &&
            (_pendingInputDrivenCheckOperation.Status == DispatcherOperationStatus.Pending || 
             _pendingInputDrivenCheckOperation.Status == DispatcherOperationStatus.Executing))
            return;

        _pendingInputDrivenCheckOperation = Application.Current.Dispatcher.InvokeAsync(
            ExecuteInputDrivenUpdateCheck,
            DispatcherPriority.ContextIdle);
    }

    private void ExecuteInputDrivenUpdateCheck()
    {
        _pendingInputDrivenCheckOperation = null;

        var shouldCheck = _hasPotentialUnnotifiedEffectMutation;
        _hasPotentialUnnotifiedEffectMutation = false;
        if (!shouldCheck) return;

        if (_appliedPresetId == null) return;

        TriggerUpdateCheck();
    }

    private bool CanRemoveGroup() =>
        SelectedGroup != null && !SelectedGroup.IsVirtual && SelectedGroup.Name != Texts.PresetManager_DefaultGroup;

    private bool CanRenameGroup(PresetGroup? group) =>
        group != null && !group.IsVirtual;

    private void ExecuteRenameGroup(PresetGroup? group)
    {
        var target = group ?? SelectedGroup;
        if (target == null || target.IsVirtual) return;
        target.BeginEdit();
        GroupRenameRequested?.Invoke(target);
    }

    private void CommitRenameGroup(PresetGroup group)
    {
        group.CommitEdit(group.Name);
        ContainerSettings.Instance.Save();
    }

    private void CancelRenameGroup(PresetGroup group)
    {
        group.CancelEdit();
    }

    private void LoadData()
    {
        Groups.Clear();
        Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupAll, IsVirtual = true });
        Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupRecent, IsVirtual = true });
        Groups.Add(new PresetGroup { Name = Texts.PresetManager_GroupFavorites, IsVirtual = true });

        if (ContainerSettings.Instance.Groups.Count == 0)
        {
            var defaultGroup = new PresetGroup { Name = Texts.PresetManager_DefaultGroup };
            defaultGroup.PresetIds.AddRange(ContainerSettings.Instance.Presets.Select(p => p.Id));
            ContainerSettings.Instance.Groups.Add(defaultGroup);
            ContainerSettings.Instance.Save();
        }

        foreach (var group in ContainerSettings.Instance.Groups)
        {
            Groups.Add(group);
        }

        SelectedGroup = Groups.FirstOrDefault(g => g.IsVirtual && g.Name == Texts.PresetManager_GroupAll);
    }

    private void RefreshDisplayedPresets()
    {
        DisplayedPresets.Clear();
        if (SelectedGroup == null) return;

        IEnumerable<EffectPreset> source;
        bool shouldSortByName = false;

        if (SelectedGroup.IsVirtual && SelectedGroup.Name == Texts.PresetManager_GroupAll)
        {
            source = ContainerSettings.Instance.Presets;
            shouldSortByName = true;
        }
        else if (SelectedGroup.IsVirtual && SelectedGroup.Name == Texts.PresetManager_GroupFavorites)
        {
            source = ContainerSettings.Instance.Presets.Where(p => p.IsFavorite);
            shouldSortByName = true;
        }
        else if (SelectedGroup.IsVirtual && SelectedGroup.Name == Texts.PresetManager_GroupRecent)
        {
            source = ContainerSettings.Instance.RecentPresetIds
                .Select(id => ContainerSettings.Instance.Presets.FirstOrDefault(p => p.Id == id))
                .OfType<EffectPreset>();
        }
        else
        {
            source = SelectedGroup.PresetIds
                .Select(id => ContainerSettings.Instance.Presets.FirstOrDefault(p => p.Id == id))
                .OfType<EffectPreset>();
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            source = SearchMode switch
            {
                PresetSearchMode.Name => source.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)),
                PresetSearchMode.EffectName => source.Where(p => ContainsEffectName(p, SearchText)),
                PresetSearchMode.EffectCount => source.Where(p => TryMatchEffectCount(p, SearchText)),
                PresetSearchMode.RawJson => source.Where(p => (p.SerializedEffects?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) || (p.SerializedTabs?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)),
                PresetSearchMode.Any => source.Where(p =>
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    ContainsEffectName(p, SearchText) ||
                    TryMatchEffectCount(p, SearchText) ||
                    (p.SerializedEffects?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.SerializedTabs?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)),
                _ => source.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)),
            };
        }

        var finalSource = shouldSortByName ? source.OrderBy(p => p.Name) : source;

        foreach (var preset in finalSource)
        {
            DisplayedPresets.Add(new PresetItemViewModel(preset));
        }
        UpdateActionCommands();
    }

    private static bool ContainsEffectName(EffectPreset preset, string searchText)
    {
        try
        {
            var state = EffectTabStateService.ResolvePresetState(preset, Texts.EffectTab_FirstName);
            return state.Tabs.Any(tab =>
            {
                var effects = EffectSerializer.Deserialize(tab.SerializedEffects);
                return effects.Any(e => e.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            });
        }
        catch
        {
            return false;
        }
    }

    private static bool TryMatchEffectCount(EffectPreset preset, string searchText)
    {
        if (!int.TryParse(searchText, out var count)) return false;
        try
        {
            var state = EffectTabStateService.ResolvePresetState(preset, Texts.EffectTab_FirstName);
            var total = state.Tabs.Sum(tab => EffectSerializer.Deserialize(tab.SerializedEffects).Count);
            return total == count;
        }
        catch
        {
            return false;
        }
    }

    private void ExecuteAddGroup()
    {
        var group = new PresetGroup { Name = $"{Texts.PresetManager_NewGroup} {ContainerSettings.Instance.Groups.Count + 1}" };
        ContainerSettings.Instance.Groups.Add(group);
        Groups.Add(group);
        ContainerSettings.Instance.Save();
        SelectedGroup = group;
    }

    private void ExecuteRemoveGroup()
    {
        if (SelectedGroup == null || !CanRemoveGroup()) return;
        ContainerSettings.Instance.Groups.Remove(SelectedGroup);
        Groups.Remove(SelectedGroup);
        ContainerSettings.Instance.Save();
        SelectedGroup = Groups.FirstOrDefault(g => g.IsVirtual && g.Name == Texts.PresetManager_GroupAll);
    }

    public void UpdateActionCommands()
    {
        ActionCommand[] commands = 
        {
            AddGroupCommand, RemoveGroupCommand, RenameGroupCommand,
            AddPresetCommand, RemovePresetCommand, UpdatePresetCommand,
            ApplyPresetCommand, RenamePresetCommand, ToggleFavoriteCommand,
            ClearUnselectedCommand, ApplySinglePresetCommand, ClearPresetCommand,
            SetSearchModeCommand, ExportPresetsCommand, ImportPresetsCommand,
            CopyPresetCommand, PastePresetCommand, CutPresetCommand
        };

        foreach (var cmd in commands)
        {
            cmd?.RaiseCanExecuteChanged();
        }
    }

    private List<PresetItemViewModel> ResolveTargets(object? parameter)
    {
        var targets = new List<PresetItemViewModel>();
        if (parameter is IList list)
            targets.AddRange(list.OfType<PresetItemViewModel>());
        else if (parameter is PresetItemViewModel vm)
            targets.Add(vm);

        if (targets.Count == 0 && SelectedPreset != null)
            targets.Add(SelectedPreset);

        return targets;
    }

    private void ExecuteAddPreset()
    {
        var targetGroup = SelectedGroup;
        if (targetGroup == null || targetGroup.IsVirtual)
        {
            targetGroup = ContainerSettings.Instance.Groups.FirstOrDefault();
            if (targetGroup == null) return;
        }

        var state = EffectTabStateService.ResolveEffectState(_effect.EffectTabsJson, _effect.Effects, Texts.EffectTab_FirstName);
        var preset = new EffectPreset
        {
            Id = Guid.NewGuid(),
            Name = Texts.PresetManager_NewPreset,
            SerializedTabs = EffectTabStateService.Serialize(state),
            SerializedEffects = EffectTabStateService.GetSelectedEffectsJson(state)
        };

        ContainerSettings.Instance.Presets.Add(preset);
        targetGroup.PresetIds.Add(preset.Id);
        ContainerSettings.Instance.Save();

        RefreshDisplayedPresets();

        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            foreach (var prop in _itemProperties)
            {
                var target = (ContainerEffect)prop.PropertyOwner;
                target.SelectedPresetJson = JsonConvert.SerializeObject(preset);
                target.PresetName = preset.Name;
            }
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
        UpdateAppliedPresetId();
        TriggerUpdateCheck();
    }

    private void ExecuteRemovePreset(object? parameter)
    {
        var presets = ResolveTargets(parameter);
        if (presets.Count == 0) return;

        foreach (var p in presets)
        {
            ContainerSettings.Instance.Presets.RemoveAll(x => x.Id == p.Model.Id);
            foreach (var g in ContainerSettings.Instance.Groups)
                g.PresetIds.Remove(p.Model.Id);
            ContainerSettings.Instance.RecentPresetIds.Remove(p.Model.Id);
        }
        ContainerSettings.Instance.Save();
        RefreshDisplayedPresets();
    }

    private void ExecuteUpdatePreset(object? parameter)
    {
        var targets = ResolveTargets(parameter);
        if (targets.Count == 0) return;
        var target = targets[0];

        var state = EffectTabStateService.ResolveEffectState(_effect.EffectTabsJson, _effect.Effects, Texts.EffectTab_FirstName);
        target.Model.SerializedTabs = EffectTabStateService.Serialize(state);
        target.Model.SerializedEffects = EffectTabStateService.GetSelectedEffectsJson(state);

        target.RefreshEffectInfo();
        ContainerSettings.Instance.Save();

        _effect.SelectedPresetJson = JsonConvert.SerializeObject(target.Model);
    }

    private void ExecuteApplyPreset(object? parameter)
    {
        var presets = ResolveTargets(parameter);
        if (presets.Count == 0) return;

        var preset = presets[0].Model;

        var recentIds = ContainerSettings.Instance.RecentPresetIds;
        recentIds.Remove(preset.Id);
        recentIds.Insert(0, preset.Id);
        while (recentIds.Count > MaxRecentPresets)
            recentIds.RemoveAt(recentIds.Count - 1);

        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            foreach (var prop in _itemProperties)
            {
                var target = (ContainerEffect)prop.PropertyOwner;
                target.SelectedPresetJson = JsonConvert.SerializeObject(preset);
                target.PresetName = preset.Name;
                target.EffectTabsJson = preset.SerializedTabs;
                var state = EffectTabStateService.ResolvePresetState(preset, Texts.EffectTab_FirstName);
                target.Effects = EffectTabStateService.GetSelectedEffects(state);
            }
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
            ContainerSettings.Instance.Save();
        }
    }

    private void ExecuteApplySinglePreset(object? parameter)
    {
        ExecuteApplyPreset(parameter);
    }

    private void ExecuteRenamePreset(object? parameter)
    {
        var targets = ResolveTargets(parameter);
        if (targets.Count == 0) return;
        var target = targets[0];

        target.BeginEdit();
        PresetRenameRequested?.Invoke(target);
    }

    public void CommitRenamePreset(PresetItemViewModel presetVm)
    {
        presetVm.CommitEdit(Texts.PresetManager_NewPreset);
        ContainerSettings.Instance.Save();
        RefreshDisplayedPresets();

        if (_appliedPresetId == presetVm.Model.Id)
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            try
            {
                foreach (var prop in _itemProperties)
                {
                    var target = (ContainerEffect)prop.PropertyOwner;
                    target.SelectedPresetJson = JsonConvert.SerializeObject(presetVm.Model);
                    target.PresetName = presetVm.Model.Name;
                }
            }
            finally
            {
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void CancelRenamePreset(PresetItemViewModel presetVm)
    {
        presetVm.CancelEdit();
    }

    public void CommitGroupRename(PresetGroup group)
    {
        CommitRenameGroup(group);
    }

    public void CancelGroupRename(PresetGroup group)
    {
        CancelRenameGroup(group);
    }

    private void ExecuteToggleFavorite(object? parameter)
    {
        var targets = ResolveTargets(parameter);
        if (targets.Count == 0) return;
        var presetVm = targets[0];

        presetVm.Model.IsFavorite = !presetVm.Model.IsFavorite;
        presetVm.RefreshFavorite();
        ContainerSettings.Instance.Save();
        if (SelectedGroup?.IsVirtual == true && SelectedGroup.Name == Texts.PresetManager_GroupFavorites)
            RefreshDisplayedPresets();
    }

    private void ExecuteClearUnselected()
    {
        var state = EffectTabStateService.ResolveEffectState(_effect.EffectTabsJson, _effect.Effects, Texts.EffectTab_FirstName);
        var selectedEffects = EffectTabStateService.GetSelectedEffects(state);
        var filtered = selectedEffects.Where(e => e.IsEnabled).ToImmutableList();

        BeginEdit?.Invoke(this, EventArgs.Empty);
        try
        {
            foreach (var prop in _itemProperties)
            {
                var target = (ContainerEffect)prop.PropertyOwner;
                target.Effects = filtered;
            }
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ExecuteClearPreset(object? parameter)
    {
        var targets = ResolveTargets(parameter);
        if (targets.Count == 0) return;
        var target = targets[0];

        var emptyState = EffectTabStateService.CreateDefault(
            ImmutableList<IVideoEffect>.Empty,
            Texts.EffectTab_FirstName);
        target.Model.SerializedTabs = EffectTabStateService.Serialize(emptyState);
        target.Model.SerializedEffects = string.Empty;
        target.RefreshEffectInfo();
        ContainerSettings.Instance.Save();
    }

    private void ExecuteSetSearchMode(object? mode)
    {
        if (mode is string modeStr && Enum.TryParse<PresetSearchMode>(modeStr, out var result))
            SearchMode = result;
    }

    private void ExecuteExportPresets(object? parameter)
    {
        var presets = ResolveTargets(parameter).Select(p => p.Model).ToList();
        if (presets.Count == 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = $"{Texts.PresetManager_ExchangeFileType}|*.zip|{Texts.PresetManager_AllFiles}|*.*",
            FileName = Texts.PresetManager_ExchangeBundleDefaultName,
            DefaultExt = ".zip"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var package = new PresetExchangePackage { Presets = presets };
            var json = JsonConvert.SerializeObject(package, Formatting.Indented, ExchangeSettings);
            using var archive = System.IO.Compression.ZipFile.Open(dialog.FileName, System.IO.Compression.ZipArchiveMode.Create);
            var entry = archive.CreateEntry("presets.json");
            using var writer = new System.IO.StreamWriter(entry.Open());
            writer.Write(json);
        }
        catch
        {
            MessageBox.Show(Texts.PresetManager_ExchangeExportError);
        }
    }

    private void ExecuteImportPresets()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = $"{Texts.PresetManager_ExchangeFileType}|*.zip|{Texts.PresetManager_AllFiles}|*.*",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(dialog.FileName);
            var entry = archive.GetEntry("presets.json");
            if (entry == null) throw new InvalidOperationException();
            using var reader = new System.IO.StreamReader(entry.Open());
            var json = reader.ReadToEnd();
            var package = JsonConvert.DeserializeObject<PresetExchangePackage>(json, ExchangeSettings);
            if (package?.Presets == null) throw new InvalidOperationException();

            var targetGroup = SelectedGroup;
            if (targetGroup == null || targetGroup.IsVirtual)
                targetGroup = ContainerSettings.Instance.Groups.FirstOrDefault();

            foreach (var preset in package.Presets)
            {
                var newPreset = new EffectPreset
                {
                    Id = Guid.NewGuid(),
                    Name = preset.Name,
                    IsFavorite = preset.IsFavorite,
                    SerializedEffects = preset.SerializedEffects,
                    SerializedTabs = preset.SerializedTabs,
                };
                ContainerSettings.Instance.Presets.Add(newPreset);
                targetGroup?.PresetIds.Add(newPreset.Id);
            }
            ContainerSettings.Instance.Save();
            RefreshDisplayedPresets();
        }
        catch
        {
            MessageBox.Show(Texts.PresetManager_ExchangeImportError);
        }
    }

    private void ExecuteCopyPreset(object? parameter)
    {
        var presets = ResolveTargets(parameter).Select(p => p.Model).ToList();
        if (presets.Count == 0) return;

        var json = JsonConvert.SerializeObject(new PresetExchangePackage { Presets = presets }, ExchangeSettings);
        Clipboard.SetText(json);
    }

    private void ExecutePastePreset()
    {
        if (!Clipboard.ContainsText()) return;
        var text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            var package = JsonConvert.DeserializeObject<PresetExchangePackage>(text, ExchangeSettings);
            if (package?.Presets == null) return;

            var targetGroup = SelectedGroup;
            if (targetGroup == null || targetGroup.IsVirtual)
                targetGroup = ContainerSettings.Instance.Groups.FirstOrDefault();

            foreach (var preset in package.Presets)
            {
                var newPreset = new EffectPreset
                {
                    Id = Guid.NewGuid(),
                    Name = preset.Name,
                    IsFavorite = preset.IsFavorite,
                    SerializedEffects = preset.SerializedEffects,
                    SerializedTabs = preset.SerializedTabs,
                };
                ContainerSettings.Instance.Presets.Add(newPreset);
                targetGroup?.PresetIds.Add(newPreset.Id);
            }
            ContainerSettings.Instance.Save();
            RefreshDisplayedPresets();
        }
        catch { }
    }

    private void ExecuteCutPreset(object? parameter)
    {
        ExecuteCopyPreset(parameter);
        ExecuteRemovePreset(parameter);
    }

    public void MoveGroup(PresetGroup source, PresetGroup target)
    {
        var srcIdx = ContainerSettings.Instance.Groups.IndexOf(source);
        var dstIdx = ContainerSettings.Instance.Groups.IndexOf(target);
        if (srcIdx < 0 || dstIdx < 0 || srcIdx == dstIdx) return;

        ContainerSettings.Instance.Groups.RemoveAt(srcIdx);
        if (srcIdx < dstIdx) dstIdx--;
        ContainerSettings.Instance.Groups.Insert(dstIdx, source);
        ContainerSettings.Instance.Save();

        var groupsSrcIdx = Groups.IndexOf(source);
        var groupsDstIdx = Groups.IndexOf(target);
        if (groupsSrcIdx >= 0 && groupsDstIdx >= 0)
            Groups.Move(groupsSrcIdx, groupsDstIdx);
    }

    public void MovePreset(PresetItemViewModel source, PresetItemViewModel target)
    {
        if (SelectedGroup == null || SelectedGroup.IsVirtual) return;

        var group = ContainerSettings.Instance.Groups.FirstOrDefault(g => g == SelectedGroup);
        if (group == null) return;

        var srcIdx = group.PresetIds.IndexOf(source.Model.Id);
        var dstIdx = group.PresetIds.IndexOf(target.Model.Id);
        if (srcIdx < 0 || dstIdx < 0 || srcIdx == dstIdx) return;

        group.PresetIds.RemoveAt(srcIdx);
        if (srcIdx < dstIdx) dstIdx--;
        group.PresetIds.Insert(dstIdx, source.Model.Id);
        ContainerSettings.Instance.Save();

        var displaySrcIdx = DisplayedPresets.IndexOf(source);
        var displayDstIdx = DisplayedPresets.IndexOf(target);
        if (displaySrcIdx >= 0 && displayDstIdx >= 0)
            DisplayedPresets.Move(displaySrcIdx, displayDstIdx);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _effect.PropertyChanged -= OnEffectPropertyChanged;
        InputManager.Current.PostProcessInput -= OnPostProcessInput;
        ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;

        foreach (var effect in _trackedEffects)
        {
            if (effect is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= OnVideoEffectPropertyChanged;
        }
    }
}
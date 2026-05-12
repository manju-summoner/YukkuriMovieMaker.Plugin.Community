using System.Collections.Immutable;
using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Settings;

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

            IDisposable? scope = _isSelfUpdating ? null : BeginUndo();
            try
            {
                SelectTabInternal(value);
                ApplyStateToContainers();
            }
            finally
            {
                scope?.Dispose();
            }
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
    public ActionCommand ClearStashesCommand { get; }
    public ActionCommand BeginEditCommand { get; }
    public ActionCommand CommitEditCommand { get; }
    public ActionCommand CancelEditCommand { get; }
    public ActionCommand MoveTabToIndexCommand { get; }
    public ActionCommand AddTemplateCommand { get; }
    public ActionCommand RestoreTemplateCommand { get; }
    public ActionCommand EditTemplateCommand { get; }
    public ActionCommand ExtractEffectCommand { get; }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;
    public event EventHandler<ConfirmationEventArgs>? ConfirmationRequested;
    public event EventHandler<TemplateDialogEventArgs>? TemplateDialogRequested;

    public ObservableCollection<EffectTabStashViewModel> Stashes { get; } = new();
    public bool HasStashes => Stashes.Count > 0;

    public ObservableCollection<EffectTabTemplateViewModel> Templates { get; } = new();
    public bool HasTemplates => Templates.Count > 0;

    public bool HasExtractEffectSources => HasStashes || HasTemplates;

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
        StashCommand = new ActionCommand(p => HasEffects(ResolveTab(p)), p => ExecuteStash(ResolveTab(p)));
        RestoreStashCommand = new ActionCommand(p => p is EffectTabStashViewModel, p => ExecuteRestoreStash(p as EffectTabStashViewModel));
        RemoveStashCommand = new ActionCommand(p => p is EffectTabStashViewModel, p => ExecuteRemoveStash(p as EffectTabStashViewModel));
        ClearStashesCommand = new ActionCommand(_ => HasStashes, _ => ExecuteClearStashes());
        BeginEditCommand = new ActionCommand(p => ResolveTab(p) != null, p => ExecuteBeginEdit(ResolveTab(p)));
        CommitEditCommand = new ActionCommand(_ => true, p => ExecuteCommitEdit(p as EffectTabItemViewModel));
        CancelEditCommand = new ActionCommand(_ => true, p => ExecuteCancelEdit(p as EffectTabItemViewModel));
        MoveTabToIndexCommand = new ActionCommand(
            p => p is MoveTabToIndexParameter param && CanMoveTabToIndex(param),
            p => ExecuteMoveTabToIndex(p as MoveTabToIndexParameter));
        AddTemplateCommand = new ActionCommand(p => HasEffects(ResolveTab(p)), p => ExecuteAddTemplate(ResolveTab(p)));
        RestoreTemplateCommand = new ActionCommand(p => p is EffectTabTemplateViewModel, p => ExecuteRestoreTemplate(p as EffectTabTemplateViewModel));
        EditTemplateCommand = new ActionCommand(p => p is EffectTabTemplateViewModel, p => ExecuteEditTemplate(p as EffectTabTemplateViewModel));
        ExtractEffectCommand = new ActionCommand(p => p is ExtractEffectViewModel, p => ExecuteExtractEffect(p as ExtractEffectViewModel));

        using (BeginSelfUpdate())
        {
            LoadTabs();
        }
        LoadStashes();
        LoadTemplates();

        _effect.PropertyChanged += OnEffectPropertyChanged;
    }

    private EffectTabItemViewModel? ResolveTab(object? param) => param as EffectTabItemViewModel ?? SelectedTab;

    private bool HasEffects(EffectTabItemViewModel? target)
        => target is not null && target.Effects.Count > 0;

    private void Tabs_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasMultipleTabs));
    }

    private void ApplyToContainers(Action<ContainerEffect> action)
    {
        foreach (var prop in _itemProperties)
            action((ContainerEffect)prop.PropertyOwner);
    }

    /// <summary>
    /// マルチセレクト中のすべての ContainerEffect に対して action を呼ぶ。
    /// 先頭の Container には sharedValue をそのまま渡し、2 個目以降には毎回 deep clone を渡す。
    /// （複数 Container に同一インスタンスを共有させるとマルチセレクト解除後の編集が
    /// 他の Container にも波及してしまうため、独立性を保つ。）
    /// </summary>
    private void ApplyToContainers<T>(T sharedValue, Action<ContainerEffect, T> action)
        where T : class
    {
        bool isFirst = true;
        foreach (var prop in _itemProperties)
        {
            var instance = isFirst
                ? sharedValue
                : (YukkuriMovieMaker.Json.Json.GetClone(sharedValue) ?? sharedValue);
            isFirst = false;
            action((ContainerEffect)prop.PropertyOwner, instance);
        }
    }

    private void LoadStashes()
    {
        EffectTabSettings.Default.Stashes.CollectionChanged += OnStashesChanged;
        SyncStashes();
    }

    private void SyncStashes()
    {
        Stashes.Clear();
        foreach (var stash in EffectTabSettings.Default.Stashes)
            Stashes.Add(new EffectTabStashViewModel(stash));
        OnPropertyChanged(nameof(HasStashes));
        OnPropertyChanged(nameof(HasExtractEffectSources));
    }

    private void OnStashesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => SyncStashes();

    private void LoadTemplates()
    {
        EffectSettings.Default.EffectTemplate.CollectionChanged += OnTemplatesChanged;
        SyncTemplates();
    }

    private void SyncTemplates()
    {
        Templates.Clear();
        foreach (var template in EffectSettings.Default.EffectTemplate)
            Templates.Add(new EffectTabTemplateViewModel(template));
        OnPropertyChanged(nameof(HasTemplates));
        OnPropertyChanged(nameof(HasExtractEffectSources));
    }

    private void OnTemplatesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => SyncTemplates();

    private void LoadTabs()
    {
        var (normalizedTabs, selectedId) = EffectTabStateService.Normalize(
            _effect.Tabs, _effect.SelectedTabId, ImmutableList<IVideoEffect>.Empty, Texts.EffectTab_FirstName);

        Tabs.Clear();
        foreach (var tab in normalizedTabs)
            Tabs.Add(new EffectTabItemViewModel(tab, this));

        UpdateIndices();

        SelectedTab = Tabs.FirstOrDefault(t => t.Id == selectedId) ?? Tabs.FirstOrDefault();
    }

    private void SelectTabInternal(EffectTabItemViewModel? tab)
    {
        _selectedTab = tab;
        OnPropertyChanged(nameof(SelectedTab));
        OnPropertyChanged(nameof(IsTabSelected));
    }

    private void OnEffectPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isSelfUpdating) return;

        if (e.PropertyName == nameof(ContainerEffect.Tabs))
        {
            using (BeginSelfUpdate())
            {
                LoadTabs();
            }
        }
    }

    /// <summary>
    /// VideoEffectSelector が EffectTab.Effects を編集した直後に呼ばれる。
    /// EffectTab 自体は INotifyPropertyChanged ではないので、ここで Tabs を新インスタンスとして
    /// 各 ContainerEffect に再代入し、Label 更新と外部監視を発火させる。
    /// </summary>
    internal void NotifyEffectsEdited()
    {
        using (BeginSelfUpdate())
        {
            ApplyStateToContainers();
        }
    }

    private void ApplyStateToContainers()
    {
        var snapshot = Tabs.Select(t => t.Model).ToImmutableList();
        var selectedId = SelectedTab?.Id;

        ApplyToContainers(snapshot, (e, tabs) =>
        {
            e.Tabs = tabs;
            e.SelectedTabId = selectedId;
        });
    }

    private void UpdateIndices()
    {
        for (int i = 0; i < Tabs.Count; i++)
            Tabs[i].Index = i;
    }

    private bool RequestConfirmation(string message, string title)
    {
        var args = new ConfirmationEventArgs(message, title);
        ConfirmationRequested?.Invoke(this, args);
        return args.Confirmed;
    }

    private void ExecuteAddTab()
    {
        using (BeginUndo())
        {
            var tab = new EffectTab { Name = string.Format(Texts.EffectTab_NumberedName, Tabs.Count + 1) };
            var vm = new EffectTabItemViewModel(tab, this);
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

        var effects = target.Effects;
        if (effects.Count == 0) return;

        // タブから退避箱への移動。元タブはこの直後 RemoveTabInternal で消えるのでインスタンス共有でよいが、
        // 復元時に独立性が要るので Clone しておく。
        var stash = new EffectTab
        {
            Id = Guid.NewGuid(),
            Name = target.Name,
            Effects = CloneEffects(effects),
        };

        EffectTabSettings.Default.Stashes.Add(stash);
        EffectTabSettings.Default.Save();

        using (BeginUndo())
        {
            RemoveTabInternal(target);
        }
    }

    private void ExecuteAddTemplate(EffectTabItemViewModel? target)
    {
        if (target == null) return;

        var effects = target.Effects;
        if (effects.Count == 0) return;

        var args = new TemplateDialogEventArgs(target.Name, false);
        TemplateDialogRequested?.Invoke(this, args);

        if (args.Result == TemplateWindowResult.Create && !string.IsNullOrWhiteSpace(args.TemplateName))
        {
            // EffectTemplate のコンストラクタが内部で Json.Json.GetClone してくれるので、ここで Clone は不要。
            var template = new EffectTemplate<IVideoEffect>(args.TemplateName, effects);
            EffectSettings.Default.EffectTemplate.Add(template);
            EffectSettings.Default.Save();
        }
    }

    private void ExecuteEditTemplate(EffectTabTemplateViewModel? templateVm)
    {
        if (templateVm == null) return;

        var args = new TemplateDialogEventArgs(templateVm.Name, true);
        TemplateDialogRequested?.Invoke(this, args);

        if (args.Result == TemplateWindowResult.Complete && !string.IsNullOrWhiteSpace(args.TemplateName))
        {
            templateVm.Name = args.TemplateName;
            EffectSettings.Default.Save();
        }
        else if (args.Result == TemplateWindowResult.Delete)
        {
            EffectSettings.Default.EffectTemplate.Remove(templateVm.Model);
            EffectSettings.Default.Save();
        }
    }

    private void ExecuteExtractEffect(ExtractEffectViewModel? p)
    {
        if (p == null || SelectedTab == null) return;

        var effectToAdd = YukkuriMovieMaker.Json.Json.GetClone(p.Effect);
        if (effectToAdd == null) return;

        using (BeginUndo())
        {
            SelectedTab.Effects = SelectedTab.Effects.Add(effectToAdd);
            ApplyStateToContainers();
        }
    }

    private void RemoveTabInternal(EffectTabItemViewModel target)
    {
        var wasSelected = SelectedTab == target;
        Tabs.Remove(target);

        if (Tabs.Count == 0)
        {
            var tab = new EffectTab { Name = Texts.EffectTab_FirstName };
            Tabs.Add(new EffectTabItemViewModel(tab, this));
        }

        UpdateIndices();

        if (wasSelected)
        {
            var next = Tabs.First();
            SelectTabInternal(next);
        }

        ApplyStateToContainers();
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
                ApplyStateToContainers();
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
            ApplyStateToContainers();
        }
    }

    private void ExecuteDuplicateTab(EffectTabItemViewModel? source)
    {
        if (source == null) return;

        using (BeginUndo())
        {
            var dup = new EffectTab
            {
                Name = source.Name + Texts.EffectTab_CopyName,
                Effects = CloneEffects(source.Effects),
            };
            var vm = new EffectTabItemViewModel(dup, this);

            var idx = Tabs.IndexOf(source);
            Tabs.Insert(idx + 1, vm);
            UpdateIndices();
            SelectedTab = vm;
        }
    }

    private void ExecuteCopy(EffectTabItemViewModel? source)
    {
        if (source == null) return;

        // クリップボードへは JSON 化するので元インスタンスとは自動的に独立する。
        var data = new EffectTab
        {
            Name = source.Name,
            Effects = source.Effects,
        };
        var json = YukkuriMovieMaker.Json.Json.GetJsonText(data);
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
            data = YukkuriMovieMaker.Json.Json.LoadFromText<EffectTab>(raw);
        }
        catch
        {
            return;
        }

        if (data == null) return;
        var pastedEffects = data.Effects ?? ImmutableList<IVideoEffect>.Empty;

        using (BeginUndo())
        {
            if (targetTabVm != null)
            {
                targetTabVm.Effects = pastedEffects;
                ApplyStateToContainers();
            }
            else
            {
                var tab = new EffectTab
                {
                    Name = data.Name + Texts.EffectTab_CopyName,
                    Effects = pastedEffects,
                };
                var vm = new EffectTabItemViewModel(tab, this);
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
            // スタッシュは復元直後に Stashes から削除されるので、移管としてインスタンスをそのまま渡す。
            // stashVm.Name は表示用に整形された文字列なので、元のタブ名は Model.Name を直接参照する。
            var tab = new EffectTab
            {
                Name = stashVm.Model.Name,
                Effects = CloneEffects(stashVm.Effects),
            };
            var vm = new EffectTabItemViewModel(tab, this);
            Tabs.Add(vm);
            UpdateIndices();
            SelectedTab = vm;
        }

        EffectTabSettings.Default.Stashes.Remove(stashVm.Model);
        EffectTabSettings.Default.Save();
    }

    private void ExecuteRemoveStash(EffectTabStashViewModel? stashVm)
    {
        if (stashVm == null) return;

        EffectTabSettings.Default.Stashes.Remove(stashVm.Model);
        EffectTabSettings.Default.Save();
    }

    private void ExecuteClearStashes()
    {
        if (!RequestConfirmation(Texts.Menu_ClearStashesConfirm, Texts.Menu_ClearStashes)) return;
        EffectTabSettings.Default.Stashes.Clear();
        EffectTabSettings.Default.Save();
    }

    private void ExecuteRestoreTemplate(EffectTabTemplateViewModel? templateVm)
    {
        if (templateVm == null) return;

        using (BeginUndo())
        {
            // ブックマークは複数回復元できる。EffectTemplate.CreateEffects() が deep clone を返す。
            var tab = new EffectTab
            {
                Name = templateVm.Name,
                Effects = templateVm.Model.CreateEffects().ToImmutableList(),
            };
            var vm = new EffectTabItemViewModel(tab, this);
            Tabs.Add(vm);
            UpdateIndices();
            SelectedTab = vm;
        }
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
            ApplyStateToContainers();
        }
    }

    private void ExecuteCancelEdit(EffectTabItemViewModel? target)
    {
        target?.CancelEdit();
    }

    private static ImmutableList<IVideoEffect> CloneEffects(ImmutableList<IVideoEffect> effects)
    {
        if (effects.IsEmpty) return ImmutableList<IVideoEffect>.Empty;
        var cloned = YukkuriMovieMaker.Json.Json.GetClone(effects);
        return cloned ?? ImmutableList<IVideoEffect>.Empty;
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
        EffectTabSettings.Default.Stashes.CollectionChanged -= OnStashesChanged;
        EffectSettings.Default.EffectTemplate.CollectionChanged -= OnTemplatesChanged;
        _effect.PropertyChanged -= OnEffectPropertyChanged;
    }
}

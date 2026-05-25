using System.Collections.Immutable;
using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

/// <summary>
/// EffectTabManagerControl の ViewModel。
///
/// 設計方針：
/// - <see cref="ContainerEffect"/>（モデル）が単一の真実の出所。VM はモデルからの一方向で構築される。
/// - VM プロパティの setter ではモデルを直接書き換えない。書き換えはすべて Command 経由で行い、
///   Command 側で <see cref="BeginUndo"/> スコープを張ってモデルを更新する。
/// - モデルの PropertyChanged を購読し、変更を検知したら <see cref="SyncFromModel"/> で VM を再構築する。
/// - Undo/Redo の実行はモデル側プロパティが直接書き換わる経路（UndoRedoPropertyChangedCommand）で行われるため、
///   VM 側に副作用ループは発生しない。
/// </summary>
internal sealed class EffectTabManagerViewModel : Bindable, IDisposable
{
    private readonly ItemProperty[] _itemProperties;
    private readonly ContainerEffect _effect;
    private const string ClipboardFormat = "YukkuriMovieMaker.Plugin.Community.Effect.Video.Container.EffectTab";

    /// <summary>
    /// true の間は <see cref="OnEffectPropertyChanged"/> での再同期をスキップする。
    /// Command の Undo スコープでモデルを複数回触る間、毎回 VM を作り直すのを防ぐためのガード。
    /// </summary>
    private bool _isSyncingFromModel;

    public ObservableCollection<EffectTabItemViewModel> Tabs { get; } = new();

    /// <summary>
    /// 選択中タブ。VM 内部および <see cref="SyncFromModel"/> 以外からは setter を呼ばない。
    /// ユーザー操作からの変更は <see cref="SelectTabCommand"/> 経由で行う。
    /// </summary>
    public EffectTabItemViewModel? SelectedTab { get; private set; }

    public bool IsTabSelected => SelectedTab != null;
    public bool HasMultipleTabs => Tabs.Count > 1;

    public ActionCommand SelectTabCommand { get; }
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

        SelectTabCommand = new ActionCommand(p => p is EffectTabItemViewModel, p => ExecuteSelectTab(p as EffectTabItemViewModel));
        AddTabCommand = new ActionCommand(_ => true, _ => ExecuteAddTab());
        RemoveTabCommand = new ActionCommand(p => ResolveTab(p) != null && HasMultipleTabs, p => ExecuteRemoveTab(ResolveTab(p)));
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

        SyncFromModel();
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
        RemoveTabCommand.RaiseCanExecuteChanged();
    }

    private void ApplyToContainers(Action<ContainerEffect> action)
    {
        foreach (var prop in _itemProperties)
            action((ContainerEffect)prop.PropertyOwner);
    }

    /// <summary>
    /// マルチセレクト中の全 <see cref="ContainerEffect"/> に対して action を呼ぶ。
    /// 先頭の Container には <paramref name="sharedValue"/> をそのまま渡し、2 個目以降には毎回 deep clone を渡す。
    /// </summary>
    private void ApplyToContainers<T>(T sharedValue, Action<ContainerEffect, T> action) where T : class
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

    /// <summary>
    /// モデル（<see cref="ContainerEffect.Tabs"/> / <see cref="ContainerEffect.SelectedTabId"/>）の
    /// 現在状態から VM の Tabs と SelectedTab を再構築する。OneWay の伝播のみで、モデルは書き換えない。
    /// </summary>
    private void SyncFromModel()
    {
        foreach (var t in Tabs)
            t.Dispose();
        Tabs.Clear();

        foreach (var tab in _effect.Tabs)
            Tabs.Add(new EffectTabItemViewModel(tab, this));

        UpdateIndices();

        var newSelected = (_effect.SelectedTabId is { } sid ? Tabs.FirstOrDefault(t => t.Id == sid) : null)
            ?? Tabs.FirstOrDefault();

        if (!ReferenceEquals(SelectedTab, newSelected))
        {
            SelectedTab = newSelected;
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(IsTabSelected));
        }
    }

    private void OnEffectPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isSyncingFromModel) return;

        // Undo/Redo の実行や、コマンド外でのモデル変更（プロジェクトロード等）に追従して VM を再構築する。
        if (e.PropertyName is nameof(ContainerEffect.Tabs) or nameof(ContainerEffect.SelectedTabId))
            SyncFromModel();
    }

    /// <summary>
    /// <see cref="YukkuriMovieMaker.Controls.VideoEffectSelector"/> が
    /// <see cref="EffectTab.Effects"/> を直接編集した直後に呼ばれる。
    /// マルチセレクト時、先頭以外の <see cref="ContainerEffect"/> の対応タブに deep clone を分配する。
    /// VideoEffectSelector 側で BeginEdit/EndEdit が中継されるので、本メソッド内で BeginUndo は張らない。
    /// Label の再評価は ContainerEffect 側の EffectTab.PropertyChanged 監視で自動的に行われる。
    /// </summary>
    internal void NotifyEffectsEdited()
    {
        if (SelectedTab == null) return;
        if (_itemProperties.Length <= 1) return;

        var sourceTab = SelectedTab.Model;
        var sourceEffects = sourceTab.Effects;

        bool isFirst = true;
        foreach (var prop in _itemProperties)
        {
            if (isFirst)
            {
                isFirst = false;
                continue;
            }
            var container = (ContainerEffect)prop.PropertyOwner;
            var matching = container.Tabs.FirstOrDefault(t => t.Id == sourceTab.Id);
            if (matching != null)
            {
                var cloned = YukkuriMovieMaker.Json.Json.GetClone(sourceEffects) ?? ImmutableList<IVideoEffect>.Empty;
                matching.Effects = cloned;
            }
        }
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

    private void ExecuteSelectTab(EffectTabItemViewModel? target)
    {
        if (target == null) return;
        if (ReferenceEquals(SelectedTab, target)) return;

        var newId = target.Id;
        using (BeginUndo())
        {
            ApplyToContainers(c => c.SelectedTabId = newId);
        }
    }

    private void ExecuteAddTab()
    {
        var newTab = new EffectTab { Name = GenerateNextTabName() };
        var newTabs = _effect.Tabs.Add(newTab);
        var newSelectedId = newTab.Id;

        using (BeginUndo())
        {
            ApplyToContainers(newTabs, (c, t) =>
            {
                c.Tabs = t;
                c.SelectedTabId = newSelectedId;
            });
        }
    }

    /// <summary>
    /// 既存タブの表示名と被らない「タブ N」形式の名前を生成する。
    /// 比較時、Model.Name が null/empty のタブは「タブ 1」相当として扱う（表示フォールバックと一致させるため）。
    /// <paramref name="excludeTabId"/> を渡すと、その ID のタブは比較対象から除外する（リネーム時の自分自身を外すため）。
    /// </summary>
    private string GenerateNextTabName(Guid? excludeTabId = null)
    {
        var existingNames = new HashSet<string>(
            _effect.Tabs
                .Where(t => excludeTabId is null || t.Id != excludeTabId)
                .Select(t => string.IsNullOrEmpty(t.Name)
                    ? string.Format(Texts.EffectTab_NumberedName, 1)
                    : t.Name));

        for (int n = 1; ; n++)
        {
            var candidate = string.Format(Texts.EffectTab_NumberedName, n);
            if (!existingNames.Contains(candidate))
                return candidate;
        }
    }

    private void ExecuteRemoveTab(EffectTabItemViewModel? target)
    {
        if (target == null || !HasMultipleTabs) return;

        using (BeginUndo())
        {
            RemoveTabFromContainers(target.Id);
        }
    }

    /// <summary>
    /// 先頭 Container を基準に Tabs から指定 ID を除き、全 Container に分配する。
    /// 1 個も残らなくなる場合は新規空タブを 1 個入れた状態にする。
    /// </summary>
    private void RemoveTabFromContainers(Guid tabId)
    {
        var current = _effect.Tabs;
        var idx = current.FindIndex(t => t.Id == tabId);
        if (idx < 0) return;

        var newTabs = current.RemoveAt(idx);

        Guid? newSelectedId;
        if (newTabs.Count == 0)
        {
            // Name は空。VM/Label 側で「タブ 1」フォールバック表示する。
            var newTab = new EffectTab();
            newTabs = newTabs.Add(newTab);
            newSelectedId = newTab.Id;
        }
        else if (_effect.SelectedTabId == tabId)
        {
            var nextIdx = idx < newTabs.Count ? idx : newTabs.Count - 1;
            newSelectedId = newTabs[nextIdx].Id;
        }
        else
        {
            newSelectedId = _effect.SelectedTabId;
        }

        ApplyToContainers(newTabs, (c, t) =>
        {
            c.Tabs = t;
            c.SelectedTabId = newSelectedId;
        });
    }

    private void ExecuteStash(EffectTabItemViewModel? target)
    {
        if (target == null) return;

        var effects = target.Effects;
        if (effects.Count == 0) return;

        // Stash 自体は EffectTabSettings.Default 側に保存し、Undo の対象外。
        // 続けてタブを削除する操作だけが Undo スタックに積まれる（現状仕様の維持）。
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
            RemoveTabFromContainers(target.Id);
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

        var targetTabId = SelectedTab.Id;
        using (BeginUndo())
        {
            // 各 Container の対応タブに deep clone した Effect を追加。
            bool isFirst = true;
            foreach (var prop in _itemProperties)
            {
                var container = (ContainerEffect)prop.PropertyOwner;
                var tab = container.Tabs.FirstOrDefault(t => t.Id == targetTabId);
                if (tab == null) continue;

                var add = isFirst
                    ? effectToAdd
                    : (YukkuriMovieMaker.Json.Json.GetClone(effectToAdd) ?? effectToAdd);
                isFirst = false;
                tab.Effects = tab.Effects.Add(add);
            }
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
        var current = _effect.Tabs;
        var idx = current.FindIndex(t => t.Id == target.Id);
        if (idx < 0) return;
        var newIdx = idx + offset;
        if (newIdx < 0 || newIdx >= current.Count) return;

        var moved = current.RemoveAt(idx).Insert(newIdx, current[idx]);

        using (BeginUndo())
        {
            ApplyToContainers(moved, (c, t) => c.Tabs = t);
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
        var current = _effect.Tabs;
        var sourceIndex = current.FindIndex(t => t.Id == param.Tab.Id);
        if (sourceIndex < 0) return;

        var adjustedIndex = param.TargetIndex > sourceIndex
            ? param.TargetIndex - 1
            : param.TargetIndex;

        if (adjustedIndex == sourceIndex) return;
        if (adjustedIndex < 0 || adjustedIndex >= current.Count) return;

        var moved = current.RemoveAt(sourceIndex).Insert(adjustedIndex, current[sourceIndex]);

        using (BeginUndo())
        {
            ApplyToContainers(moved, (c, t) => c.Tabs = t);
        }
    }

    private void ExecuteDuplicateTab(EffectTabItemViewModel? source)
    {
        if (source == null) return;

        var srcId = source.Id;
        var current = _effect.Tabs;
        var srcIdx = current.FindIndex(t => t.Id == srcId);
        if (srcIdx < 0) return;

        var srcTab = current[srcIdx];
        var dup = new EffectTab
        {
            Name = srcTab.Name + Texts.EffectTab_CopyName,
            Effects = CloneEffects(srcTab.Effects),
        };
        var newTabs = current.Insert(srcIdx + 1, dup);
        var newSelectedId = dup.Id;

        using (BeginUndo())
        {
            ApplyToContainers(newTabs, (c, t) =>
            {
                c.Tabs = t;
                c.SelectedTabId = newSelectedId;
            });
        }
    }

    private void ExecuteCopy(EffectTabItemViewModel? source)
    {
        if (source == null) return;

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
                // 指定タブの Effects を貼り付け内容で置き換える。
                var targetId = targetTabVm.Id;
                bool isFirst = true;
                foreach (var prop in _itemProperties)
                {
                    var container = (ContainerEffect)prop.PropertyOwner;
                    var tab = container.Tabs.FirstOrDefault(t => t.Id == targetId);
                    if (tab == null) continue;

                    var effects = isFirst
                        ? pastedEffects
                        : (YukkuriMovieMaker.Json.Json.GetClone(pastedEffects) ?? ImmutableList<IVideoEffect>.Empty);
                    isFirst = false;
                    tab.Effects = effects;
                }
            }
            else
            {
                // 新規タブとして追加。
                var newTab = new EffectTab
                {
                    Name = data.Name + Texts.EffectTab_CopyName,
                    Effects = pastedEffects,
                };
                var newTabs = _effect.Tabs.Add(newTab);
                var newSelectedId = newTab.Id;

                ApplyToContainers(newTabs, (c, t) =>
                {
                    c.Tabs = t;
                    c.SelectedTabId = newSelectedId;
                });
            }
        }
    }

    private void ExecuteRestoreStash(EffectTabStashViewModel? stashVm)
    {
        if (stashVm == null) return;

        var newTab = new EffectTab
        {
            Name = stashVm.Model.Name,
            Effects = CloneEffects(stashVm.Effects),
        };
        var newTabs = _effect.Tabs.Add(newTab);
        var newSelectedId = newTab.Id;

        using (BeginUndo())
        {
            ApplyToContainers(newTabs, (c, t) =>
            {
                c.Tabs = t;
                c.SelectedTabId = newSelectedId;
            });
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

        var newTab = new EffectTab
        {
            Name = templateVm.Name,
            Effects = templateVm.Model.CreateEffects().ToImmutableList(),
        };
        var newTabs = _effect.Tabs.Add(newTab);
        var newSelectedId = newTab.Id;

        using (BeginUndo())
        {
            ApplyToContainers(newTabs, (c, t) =>
            {
                c.Tabs = t;
                c.SelectedTabId = newSelectedId;
            });
        }
    }

    private void ExecuteBeginEdit(EffectTabItemViewModel? target)
    {
        target?.BeginEditing();
    }

    private void ExecuteCommitEdit(EffectTabItemViewModel? target)
    {
        if (target == null || !target.IsEditing) return;

        // 空入力時：他のタブと表示が被るかチェックする。
        // 「タブ 1」が空くなら Model.Name は空のまま（表示フォールバックで「タブ 1」になる）、
        // すでに他に「タブ 1」相当があるなら被らない最小番号を Model.Name に焼く。
        var trimmed = target.EditName?.Trim() ?? string.Empty;
        var targetId = target.Id;
        string newName;
        if (string.IsNullOrEmpty(trimmed))
        {
            var firstFallback = string.Format(Texts.EffectTab_NumberedName, 1);
            var generated = GenerateNextTabName(targetId);
            newName = generated == firstFallback ? string.Empty : generated;
        }
        else
        {
            newName = trimmed;
        }
        var oldName = target.Model.Name;

        if (newName != oldName)
        {
            using (BeginUndo())
            {
                // 各 Container の対応タブの Name を更新。Name は値型相当なので deep clone 不要。
                foreach (var prop in _itemProperties)
                {
                    var container = (ContainerEffect)prop.PropertyOwner;
                    var tab = container.Tabs.FirstOrDefault(t => t.Id == targetId);
                    if (tab != null) tab.Name = newName;
                }
            }
        }

        target.EndEditing();
    }

    private void ExecuteCancelEdit(EffectTabItemViewModel? target)
    {
        target?.EndEditing();
    }

    private static ImmutableList<IVideoEffect> CloneEffects(ImmutableList<IVideoEffect> effects)
    {
        if (effects.IsEmpty) return ImmutableList<IVideoEffect>.Empty;
        var cloned = YukkuriMovieMaker.Json.Json.GetClone(effects);
        return cloned ?? ImmutableList<IVideoEffect>.Empty;
    }

    private IDisposable BeginUndo() => new UndoScope(this);

    /// <summary>
    /// Command がモデルを更新するためのスコープ。
    /// - BeginEdit/EndEdit イベントを発火し、Recorder に Undo の塊として記録させる。
    /// - スコープ内では <see cref="OnEffectPropertyChanged"/> の再同期をガードして、
    ///   モデル更新を複数回したときに毎回 VM を作り直すのを防ぐ。
    /// - スコープを抜けたところで一度だけ <see cref="SyncFromModel"/> を呼ぶ。
    /// </summary>
    private sealed class UndoScope : IDisposable
    {
        private readonly EffectTabManagerViewModel _vm;
        private readonly bool _prevSyncing;

        public UndoScope(EffectTabManagerViewModel vm)
        {
            _vm = vm;
            _prevSyncing = _vm._isSyncingFromModel;
            _vm._isSyncingFromModel = true;
            _vm.BeginEdit?.Invoke(_vm, EventArgs.Empty);
        }

        public void Dispose()
        {
            _vm._isSyncingFromModel = _prevSyncing;
            _vm.SyncFromModel();
            _vm.EndEdit?.Invoke(_vm, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        foreach (var t in Tabs)
            t.Dispose();
        Tabs.CollectionChanged -= Tabs_CollectionChanged;
        EffectTabSettings.Default.Stashes.CollectionChanged -= OnStashesChanged;
        EffectSettings.Default.EffectTemplate.CollectionChanged -= OnTemplatesChanged;
        _effect.PropertyChanged -= OnEffectPropertyChanged;
    }
}

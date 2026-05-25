using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class EffectTabItemViewModel : Bindable, IDisposable
{
    public EffectTab Model { get; }
    public EffectTabManagerViewModel Manager { get; }

    public Guid Id => Model.Id;

    /// <summary>
    /// 表示用のタブ名。Model.Name が null/empty の場合は「タブ 1」表記にフォールバックする。
    /// </summary>
    public string Name => string.IsNullOrEmpty(Model.Name) ? string.Format(Texts.EffectTab_NumberedName, 1) : Model.Name;

    public ImmutableList<IVideoEffect> Effects => Model.Effects;

    public int Index
    {
        get => field;
        set => Set(ref field, value, nameof(Index), nameof(IndexLabel), nameof(CompactLabel));
    }

    public string IndexLabel => (Index + 1).ToString(CultureInfo.InvariantCulture);

    public string CompactLabel
    {
        get
        {
            var trimmed = Name?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                return StringInfo.GetNextTextElement(trimmed, 0);

            return IndexLabel;
        }
    }

    public bool IsEditing
    {
        get => field;
        set => Set(ref field, value, nameof(IsEditing));
    }

    public string EditName
    {
        get => field;
        set => Set(ref field, value, nameof(EditName));
    } = string.Empty;


    public EffectTabItemViewModel(EffectTab model, EffectTabManagerViewModel manager)
    {
        Model = model;
        Manager = manager;
        EditName = model.Name;
        Model.PropertyChanged += OnModelPropertyChanged;
    }

    /// <summary>
    /// Model 側プロパティの変更を VM へ OneWay で伝播する。
    /// VM は Model を直接書き換えない（Command 経由でのみ更新される）。
    /// </summary>
    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EffectTab.Name):
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(CompactLabel));
                break;
            case nameof(EffectTab.Effects):
                OnPropertyChanged(nameof(Effects));
                break;
        }
    }
    internal void BeginEditing()
    {
        EditName = Name;
        IsEditing = true;
    }

    internal void EndEditing()
    {
        EditName = Name;
        IsEditing = false;
    }

    public void Dispose()
    {
        Model.PropertyChanged -= OnModelPropertyChanged;
    }
}

using System.Collections.Immutable;
using System.Globalization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class EffectTabItemViewModel : Bindable
{
    public EffectTab Model { get; }
    public EffectTabManagerViewModel Manager { get; }

    public EffectTabItemViewModel(EffectTab model, EffectTabManagerViewModel manager)
    {
        Model = model;
        Manager = manager;
        _editName = model.Name;
    }

    public Guid Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name == value) return;
            Model.Name = value;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(CompactLabel));
        }
    }

    public ImmutableList<IVideoEffect> Effects
    {
        get => Model.Effects;
        set
        {
            if (ReferenceEquals(Model.Effects, value)) return;
            Model.Effects = value;
            OnPropertyChanged(nameof(Effects));
        }
    }

    private int _index;
    public int Index
    {
        get => _index;
        set
        {
            if (!Set(ref _index, value)) return;
            OnPropertyChanged(nameof(IndexLabel));
            OnPropertyChanged(nameof(CompactLabel));
        }
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

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    private string _editName;
    public string EditName
    {
        get => _editName;
        set => Set(ref _editName, value);
    }

    public void BeginEdit()
    {
        EditName = Name;
        IsEditing = true;
    }

    public void CommitEdit(string fallbackName)
    {
        var next = string.IsNullOrWhiteSpace(EditName) ? fallbackName : EditName.Trim();
        Name = next;
        EditName = next;
        IsEditing = false;
    }

    public void CancelEdit()
    {
        EditName = Name;
        IsEditing = false;
    }
}

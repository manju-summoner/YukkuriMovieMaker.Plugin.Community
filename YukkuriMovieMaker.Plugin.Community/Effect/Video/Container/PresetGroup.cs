using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class PresetGroup : Bindable
{
    private string _name = string.Empty;
    private bool _isEditing;
    private string _editName = string.Empty;

    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonIgnore]
    public bool IsVirtual { get; init; }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    [JsonIgnore]
    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    [JsonIgnore]
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

    public List<Guid> PresetIds { get; set; } = [];
}

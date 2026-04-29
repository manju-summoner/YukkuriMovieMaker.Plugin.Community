using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class PresetItemViewModel : Bindable
{
    public EffectPreset Model { get; }

    public string Name => Model.Name;
    public bool IsFavorite => Model.IsFavorite;

    private int _effectCount;
    public int EffectCount
    {
        get => _effectCount;
        private set => Set(ref _effectCount, value);
    }

    private string _effectInfo = string.Empty;
    public string EffectInfo
    {
        get => _effectInfo;
        private set => Set(ref _effectInfo, value);
    }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    private string _editName = string.Empty;
    public string EditName
    {
        get => _editName;
        set => Set(ref _editName, value);
    }

    public PresetItemViewModel(EffectPreset model)
    {
        Model = model;
        _editName = model.Name;
        RefreshEffectInfo();
    }

    public void RefreshName() => OnPropertyChanged(nameof(Name));

    public void RefreshFavorite() => OnPropertyChanged(nameof(IsFavorite));

    public void BeginEdit()
    {
        EditName = Model.Name;
        IsEditing = true;
    }

    public void CommitEdit(string fallbackName)
    {
        var next = string.IsNullOrWhiteSpace(EditName) ? fallbackName : EditName.Trim();
        Model.Name = next;
        EditName = next;
        IsEditing = false;
        OnPropertyChanged(nameof(Name));
    }

    public void CancelEdit()
    {
        EditName = Model.Name;
        IsEditing = false;
    }

    public void RefreshEffectInfo()
    {
        try
        {
            var state = EffectTabStateService.ResolvePresetState(Model, Texts.EffectTab_FirstName);
            var blocks = new List<string>(state.Tabs.Count);
            var totalCount = 0;
            var maxEffectNameLength = 1;

            foreach (var tab in state.Tabs)
            {
                var effects = EffectSerializer.Deserialize(tab.SerializedEffects);
                totalCount += effects.Count;

                foreach (var effect in effects)
                {
                    var len = GetTextElementLength(effect.Label);
                    if (len > maxEffectNameLength)
                        maxEffectNameLength = len;
                }

                var lines = new List<string> { $"[{tab.Name}]" };
                if (effects.Count == 0)
                    lines.Add("-");
                else
                    lines.AddRange(effects.Select(e => e.Label));

                blocks.Add(string.Join("\n", lines));
            }

            EffectCount = totalCount;
            var separator = new string('─', Math.Max(1, maxEffectNameLength));
            EffectInfo = string.Join($"\n{separator}\n", blocks);
        }
        catch
        {
            EffectCount = 0;
            EffectInfo = string.Empty;
        }
    }

    private static int GetTextElementLength(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return StringInfo.ParseCombiningCharacters(text).Length;
    }
}

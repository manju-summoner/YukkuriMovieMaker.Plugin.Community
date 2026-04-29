using System;
using System.Collections.Generic;
using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public class ContainerSettings : SettingsBase<ContainerSettings>
{
    public override string Name => Texts.VideoEffect_Name;
    public override SettingsCategory Category => SettingsCategory.VideoEffect;
    public override bool HasSettingView => false;
    public override object SettingView => null!;

    public static ContainerSettings Instance => Default;

    public List<EffectPreset> Presets { get; set; } = new();
    public List<PresetGroup> Groups { get; set; } = new();
    public List<Guid> RecentPresetIds { get; set; } = new();
    public double ControlHeight { get; set; } = 300.0;
    public double GroupColumnWidth { get; set; } = 200.0;

    public override void Initialize()
    {
    }
}

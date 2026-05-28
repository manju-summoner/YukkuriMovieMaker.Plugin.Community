using System.Collections.Immutable;
using System.Text;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class EffectTabTemplateViewModel(EffectTemplate<IVideoEffect> model) : Bindable
{
    public EffectTemplate<IVideoEffect> Model { get; } = model;

    public string Name
    {
        get => Model.Name ?? string.Empty;
        set
        {
            if (Model.Name == value) return;
            Model.Name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public ImmutableList<IVideoEffect> Effects => Model.Effects.ToImmutableList();

    public IEnumerable<ExtractEffectViewModel> ExtractEffects =>
        Model.Effects.Select(e => new ExtractEffectViewModel(e)).ToList();

    public string ToolTipText
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var effect in Model.Effects)
                sb.AppendLine(effect.Label);
            return sb.ToString().TrimEnd();
        }
    }
}

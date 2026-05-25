using System.Collections.Immutable;
using System.Text;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public abstract class EffectTabBaseViewModel(EffectTab model) : Bindable
{
    public EffectTab Model { get; } = model;

    public Guid Id => Model.Id;

    public ImmutableList<IVideoEffect> Effects => Model.Effects;

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

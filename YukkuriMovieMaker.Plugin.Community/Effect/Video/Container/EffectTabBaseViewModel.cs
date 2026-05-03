using System.Text;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public abstract class EffectTabBaseViewModel : Bindable
{
    public EffectTab Model { get; }

    protected EffectTabBaseViewModel(EffectTab model)
    {
        Model = model;
    }

    public Guid Id => Model.Id;

    public string SerializedEffects => Model.SerializedEffects;

    public IEnumerable<ExtractEffectViewModel> ExtractEffects =>
        EffectSerializer.Deserialize(SerializedEffects)
            .Select(e => new ExtractEffectViewModel(e))
            .ToList();

    public string ToolTipText
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var effect in EffectSerializer.Deserialize(SerializedEffects))
                sb.AppendLine(effect.Label);
            return sb.ToString().TrimEnd();
        }
    }
}

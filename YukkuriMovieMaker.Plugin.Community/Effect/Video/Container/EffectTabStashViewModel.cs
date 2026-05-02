using System.Text;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public class EffectTabStashViewModel : Bindable
{
    public EffectTab Model { get; }

    public EffectTabStashViewModel(EffectTab model)
    {
        Model = model;
    }

    public Guid Id => Model.Id;

    public string Name => Model.Name;

    public string SerializedEffects => Model.SerializedEffects;

    public string ToolTipText
    {
        get
        {
            var effects = EffectSerializer.Deserialize(SerializedEffects);
            var sb = new StringBuilder();
            foreach (var effect in effects)
            {
                sb.AppendLine(effect.Label);
            }
            return sb.ToString().TrimEnd();
        }
    }
}

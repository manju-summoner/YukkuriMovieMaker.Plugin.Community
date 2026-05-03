using System.Text;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public class EffectTabBookmarkViewModel : Bindable
{
    public EffectTab Model { get; }

    public EffectTabBookmarkViewModel(EffectTab model)
    {
        Model = model;
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
        }
    }

    public string SerializedEffects => Model.SerializedEffects;

    public IEnumerable<ExtractEffectViewModel> ExtractEffects
    {
        get
        {
            var effects = EffectSerializer.Deserialize(SerializedEffects);
            return effects.Select(e => new ExtractEffectViewModel(e)).ToList();
        }
    }

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

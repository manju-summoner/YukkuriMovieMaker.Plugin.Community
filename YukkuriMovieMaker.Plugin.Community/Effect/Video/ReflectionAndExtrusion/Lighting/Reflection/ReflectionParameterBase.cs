using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.Reflection
{
    internal abstract class ReflectionParameterBase : Animatable
    {
        [Display(Name = nameof(Texts.ConstantName), Description = nameof(Texts.ConstantDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Constant { get; } = new Animation(50, 0, 1000000);

        [Display(Name = nameof(Texts.Color), Description = nameof(Texts.Color), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        [Display(Name = nameof(Texts.BlendModeName), Description = nameof(Texts.BlendModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public Blend Blend { get => blend; set => Set(ref blend, value); }
        Blend blend = Blend.Add;

        public abstract IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Constant];

        public class ReflectionParameterBaseSharedData
        {
            public Animation Constant { get; } = new Animation(50, 0, 1000000);
            public Color Color { get; set; }
            public Blend Blend { get; set; }
            public ReflectionParameterBaseSharedData()
            {

            }
            public ReflectionParameterBaseSharedData(ReflectionParameterBase parameter)
            {
                Constant.CopyFrom(parameter.Constant);
                Color = parameter.Color;
                Blend = parameter.Blend;
            }
            public void CopyTo(ReflectionParameterBase parameter)
            {
                parameter.Constant.CopyFrom(Constant);
                parameter.Color = Color;
                parameter.Blend = Blend;
            }
        }
    }
}

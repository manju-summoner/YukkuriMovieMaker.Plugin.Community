using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Brush.Commons;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular
{
    internal abstract class DiffuseAndSpecularEffectBase : VideoEffectBase
    {
        public override string Label => Texts.PointLight;


        [Display(GroupName = nameof(Texts.Diffuse), Name = nameof(Texts.ConstantName), Description = nameof(Texts.ConstantDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation DiffuseConstant { get; } = new Animation(50, 0, 1000000);

        [Display(GroupName = nameof(Texts.Diffuse), Name = nameof(Texts.Color), Description = nameof(Texts.Color), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color DiffuseColor { get => diffuseColor; set => Set(ref diffuseColor, value); }
        Color diffuseColor = Colors.White;

        [Display(GroupName = nameof(Texts.Diffuse), Name = nameof(Texts.BlendModeName), Description = nameof(Texts.BlendModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public Project.Blend DiffuseBlend { get => diffuseBlend; set => Set(ref diffuseBlend, value); }
        Project.Blend diffuseBlend = Project.Blend.Add;



        [Display(GroupName = nameof(Texts.Specular), Name = nameof(Texts.ConstantName), Description = nameof(Texts.ConstantDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation SpecularConstant { get; } = new Animation(50, 0, 1000000);

        [Display(GroupName = nameof(Texts.Specular), Name = nameof(Texts.ExponentName), Description = nameof(Texts.ExponentDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 1, 128)]
        public Animation SpecularExponent { get; } = new Animation(1, 1, 128);

        [Display(GroupName = nameof(Texts.Specular), Name = nameof(Texts.Color), Description = nameof(Texts.Color), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color SpecularColor { get => specularColor; set => Set(ref specularColor, value); }
        Color specularColor = Colors.White;

        [Display(GroupName = nameof(Texts.Specular), Name = nameof(Texts.BlendModeName), Description = nameof(Texts.BlendModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public Project.Blend SpecularBlend { get => specularBlend; set => Set(ref specularBlend, value); }
        Project.Blend specularBlend = Project.Blend.Add;



        [Display(GroupName = nameof(Texts.Hightmap), Name = nameof(Texts.FileName), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(Settings.FileGroupType.Texture, ShowThumbnail = true)]
        public string? FilePath { get => filePath; set => Set(ref filePath, value); }
        string? filePath = null;

        [Display(GroupName = nameof(Texts.Hightmap), Name = nameof(Texts.SurfaceScale), Description = nameof(Texts.SurfaceScale), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation SurfaceScale { get; } = new Animation(100, 0, 10000);

        [Display(GroupName = nameof(Texts.Hightmap), Name = nameof(Texts.Zoom), Description = nameof(Texts.Zoom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation Zoom { get; } = new Animation(100, 1, 5000);

        [Display(GroupName = nameof(Texts.Hightmap), Name = nameof(Texts.BlurRadius), Description = nameof(Texts.BlurRadius), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Blur { get; } = new Animation(10, 0, 250 * 3);

        [Display(GroupName = nameof(Texts.Hightmap), Name = nameof(Texts.Invert), Description = nameof(Texts.Invert), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsInvert { get => isInvert; set => Set(ref isInvert, value); }
        bool isInvert = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;

            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={SpecularConstant.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={SpecularExponent.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={DiffuseConstant.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track2={SurfaceScale.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=拡散鏡面反射（設定1）@YMM4-未実装\r\n" +
                $"param=" +
                    $"local specularColor={SpecularColor.R:X2}{SpecularColor.G:X2}{SpecularColor.B:X2};" +
                    $"local diffuseColor={DiffuseColor.R:X2}{DiffuseColor.G:X2}{DiffuseColor.B:X2};" +
                    $"local specularBlend={(int)SpecularBlend};" +
                    $"local diffuseBlend={(int)DiffuseBlend};" +
                    $"local invert={(IsInvert ? 0 : 1)};" +
                    $"\r\n";

            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={SurfaceScale.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track1={Zoom.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track2={Blur.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=拡散鏡面反射（設定2）@YMM4-未実装\r\n" +
                $"param=" +
                    $"local file={FilePath};" +
                    $"\r\n";
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [SpecularConstant, SpecularExponent, DiffuseConstant, SurfaceScale, Zoom, Blur];
    }
}

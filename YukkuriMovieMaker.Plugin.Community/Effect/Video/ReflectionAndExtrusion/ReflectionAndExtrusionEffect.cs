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
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.Bevel;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.DistantDiffuse;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion
{
    [VideoEffect(nameof(Texts.ReflectionAndExtrusion), [VideoEffectCategories.Decoration], ["鏡面", "拡散", "スペキュラ", "ディフューズ", "specular", "diffuse", "反射", "リフレクト", "リフレクション", "reflection", "光源", "ライト", "ライティング", "lighting", "平行光源", "ディスタントライト", "distant light", "点光源", "ポイントライト", "point light", "高さ場", "ハイトマップ", "heightmap", "面取り", "ベベル", "bevel", "バンプマップ", "バンプマッピング", "bump mapping"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class ReflectionAndExtrusionEffect : VideoEffectBase, IFileItem
    {
        public override string Label => Texts.ReflectionAndExtrusion;


        [Display(GroupName = nameof(Texts.Reflection), Name = nameof(Texts.Mode), Description = nameof(Texts.Mode), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public LightingMode LightingMode { get => lightingMode; set => Set(ref lightingMode, value); }
        LightingMode lightingMode = LightingMode.DistantDiffuse;

        [Display(AutoGenerateField = true)]
        public LightingParameterBase Lighting { get=> lighting; set => Set(ref lighting, value); }
        LightingParameterBase lighting = new DistantDiffuseLightingParameter();


        [Display(GroupName = nameof(Texts.Extrusion), Name = nameof(Texts.Mode), Description = nameof(Texts.Mode), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public HeightmapMode HeightmapMode { get => heightmapMode; set => Set(ref heightmapMode, value); }
        HeightmapMode heightmapMode = HeightmapMode.Bevel;

        [Display(GroupName = nameof(Texts.Extrusion), AutoGenerateField = true, ResourceType = typeof(Texts))]
        public HeightmapParameterBase Heightmap { get => heightmap; set => Set(ref heightmap, value); }
        HeightmapParameterBase heightmap = new BevelHeightmapParameter();

        [Display(GroupName = nameof(Texts.Extrusion), Name = nameof(Texts.BlurRadius), ResourceType = typeof(Texts), Order = 20)]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Blur { get; } = new(10
            , 0, 250 * 3);

        [Display(GroupName = nameof(Texts.Extrusion), Name = nameof(Texts.Invert), Description = nameof(Texts.Invert), ResourceType = typeof(Texts), Order = 30)]
        [ToggleSlider]
        public bool IsInvert { get => isInvert; set => Set(ref isInvert, value); }
        bool isInvert = false;

        [Display(GroupName = nameof(Texts.Reflection), Name = nameof(Texts.LinearHdrComposite), Description = nameof(Texts.LinearHdrCompositeDesc), ResourceType = typeof(Texts), Order = 40)]
        [ToggleSlider]
        public bool IsLinearHdrCompositeEnabled { get => isLinearHdrCompositeEnabled; set => Set(ref isLinearHdrCompositeEnabled, value); }
        bool isLinearHdrCompositeEnabled;

        [Display(GroupName = nameof(Texts.Occlusion), Name = nameof(Texts.SelfShadowStrength), Description = nameof(Texts.SelfShadowDesc), ResourceType = typeof(Texts), Order = 50)]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation SelfShadowStrength { get; } = new(0, 0, 100);

        [Display(GroupName = nameof(Texts.Occlusion), Name = nameof(Texts.AmbientOcclusionStrength), Description = nameof(Texts.AmbientOcclusionDesc), ResourceType = typeof(Texts), Order = 51)]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation AmbientOcclusionStrength { get; } = new(0, 0, 100);

        [Display(GroupName = nameof(Texts.Occlusion), Name = nameof(Texts.OcclusionDistance), ResourceType = typeof(Texts), Order = 52)]
        [AnimationSlider("F1", "px", 1, 128)]
        public Animation OcclusionDistance { get; } = new(16, 1, 128);

        [Display(GroupName = nameof(Texts.Occlusion), Name = nameof(Texts.OcclusionBias), ResourceType = typeof(Texts), Order = 53)]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation OcclusionBias { get; } = new(0.5, 0, 10);

        [Display(GroupName = nameof(Texts.Occlusion), Name = nameof(Texts.OcclusionSoftness), ResourceType = typeof(Texts), Order = 54)]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation OcclusionSoftness { get; } = new(1, 0, 10);

        [Display(GroupName = nameof(Texts.Occlusion), Name = nameof(Texts.OcclusionQuality), ResourceType = typeof(Texts), Order = 55)]
        [EnumComboBox]
        public OcclusionQuality OcclusionQuality { get => occlusionQuality; set => Set(ref occlusionQuality, value); }
        OcclusionQuality occlusionQuality = OcclusionQuality.Medium;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;

            foreach(var filter in Lighting.CreateExoVideoFilters(IsEnabled, keyFrameIndex, exoOutputDescription))
                yield return filter;

            foreach (var filter in Heightmap.CreateExoVideoFilters(IsEnabled, keyFrameIndex, exoOutputDescription))
                yield return filter;

            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Blur.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=反射と立体化@YMM4-未実装\r\n" +
                $"param=" +
                    $"local invert={(IsInvert ? 0 : 1)};" +
                    $"\r\n";
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            Lighting, Heightmap, Blur, SelfShadowStrength, AmbientOcclusionStrength,
            OcclusionDistance, OcclusionBias, OcclusionSoftness,
        ];

        public override void BeginEdit()
        {
            base.BeginEdit();
        }

        public override ValueTask EndEditAsync()
        {
            Lighting = LightingMode.Convert(Lighting);
            Heightmap = HeightmapMode.Convert(Heightmap);

            return base.EndEditAsync();
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new ReflectionAndExtrusionEffectProcessor(devices, this);
        }

        public override IEnumerable<TimelineResource> GetResources()
        {
            foreach(var resource in base.GetResources())
                yield return resource;
            if(Heightmap is IResourceItem heightmapResourceItem)
                foreach(var resource in heightmapResourceItem.GetResources())
                    yield return resource;
        }

        public override IEnumerable<string> GetFiles()
        {
            foreach(var file in base.GetFiles())
                yield return file;
            if(Heightmap is IFileItem heightmapFileItem)
                foreach(var file in heightmapFileItem.GetFiles())
                    yield return file;
        }

        public override void ReplaceFile(string from, string to)
        {
            base.ReplaceFile(from, to);
            if(Heightmap is IFileItem heightmapFileItem)
                heightmapFileItem.ReplaceFile(from, to);
        }
    }
}

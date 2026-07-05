using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PixelSort
{
    /// <summary>
    /// ピクセルソートエフェクト。
    /// 輝度がしきい値範囲内の連続ピクセル区間を方向軸に沿って検出し、
    /// 区間内のピクセルを輝度順に並べ替えるグリッチアート表現。
    /// </summary>
    [VideoEffect(nameof(Texts.PixelSortEffectName), [VideoEffectCategories.Filtering], ["ピクセルソート", "pixel sort", "pixelsort", "グリッチ", "glitch", "ソート", "sort", "datamosh"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    class PixelSortEffect : VideoEffectBase
    {
        public override string Label => Texts.PixelSortEffectName;

        [Display(GroupName = nameof(Texts.PixelSortEffectName), Name = nameof(Texts.PixelSortEffectDirectionName), Description = nameof(Texts.PixelSortEffectDirectionDesc), Order = 10, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public PixelSortDirection Direction { get => direction; set => Set(ref direction, value); }
        PixelSortDirection direction = PixelSortDirection.Down;

        [Display(GroupName = nameof(Texts.PixelSortEffectName), Name = nameof(Texts.PixelSortEffectThresholdLowName), Description = nameof(Texts.PixelSortEffectThresholdLowDesc), Order = 20, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation ThresholdLow { get; } = new Animation(30, 0, 100);

        [Display(GroupName = nameof(Texts.PixelSortEffectName), Name = nameof(Texts.PixelSortEffectThresholdHighName), Description = nameof(Texts.PixelSortEffectThresholdHighDesc), Order = 30, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation ThresholdHigh { get; } = new Animation(90, 0, 100);

        //既定値は最大(=シェーダーが扱える画像サイズ上限)。この場合、区間はしきい値と画像端でのみ区切られる
        [Display(GroupName = nameof(Texts.PixelSortEffectName), Name = nameof(Texts.PixelSortEffectSpanLengthName), Description = nameof(Texts.PixelSortEffectSpanLengthDesc), Order = 40, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 8, YMM4Constants.MaximumShaderImageSize)]
        public Animation SpanLength { get; } = new Animation(YMM4Constants.MaximumShaderImageSize, 8, YMM4Constants.MaximumShaderImageSize);

        [Display(GroupName = nameof(Texts.PixelSortEffectName), Name = nameof(Texts.PixelSortEffectStrengthName), Description = nameof(Texts.PixelSortEffectStrengthDesc), Order = 50, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(100, 0, 100);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するフィルタが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new PixelSortEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [ThresholdLow, ThresholdHigh, SpanLength, Strength];
    }
}

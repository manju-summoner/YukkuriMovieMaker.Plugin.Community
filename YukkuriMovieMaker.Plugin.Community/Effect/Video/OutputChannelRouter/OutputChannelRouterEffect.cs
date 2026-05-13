using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputChannelRouter
{
    [VideoEffect(nameof(Texts.ChannelRouterEffectName), [VideoEffectCategories.Filtering], ["channel router", "channel combiner", "set channel", "composite channels", "channel composite", "チャンネルルーター", "分岐とチャンネル合成", "チャンネル合成", "通道路由", "通道合成", "채널 라우터", "채널 합성", "CustomValue"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class OutputChannelRouterEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.ChannelRouterEffectName} {TargetIndex}";

        [Display(GroupName = nameof(Texts.ChannelRouterEffectName), Name = nameof(Texts.TargetIndexName), Description = nameof(Texts.TargetIndexDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 16)]
        [Range(0, 1024)]
        [DefaultValue(1)]
        public int TargetIndex
        {
            get => _targetIndex;
            set => Set(ref _targetIndex, value, nameof(TargetIndex), nameof(Label));
        }
        private int _targetIndex = 1;

        [Display(GroupName = nameof(Texts.ChannelRouterEffectName), Name = nameof(Texts.OutputRName), Description = nameof(Texts.OutputRDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ChannelSource OutputR
        {
            get => _outputR;
            set => Set(ref _outputR, value);
        }
        private ChannelSource _outputR = ChannelSource.CurrentR;

        [Display(GroupName = nameof(Texts.ChannelRouterEffectName), Name = nameof(Texts.OutputGName), Description = nameof(Texts.OutputGDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ChannelSource OutputG
        {
            get => _outputG;
            set => Set(ref _outputG, value);
        }
        private ChannelSource _outputG = ChannelSource.CurrentG;

        [Display(GroupName = nameof(Texts.ChannelRouterEffectName), Name = nameof(Texts.OutputBName), Description = nameof(Texts.OutputBDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ChannelSource OutputB
        {
            get => _outputB;
            set => Set(ref _outputB, value);
        }
        private ChannelSource _outputB = ChannelSource.CurrentB;

        [Display(GroupName = nameof(Texts.ChannelRouterEffectName), Name = nameof(Texts.OutputAName), Description = nameof(Texts.OutputADesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ChannelSource OutputA
        {
            get => _outputA;
            set => Set(ref _outputA, value);
        }
        private ChannelSource _outputA = ChannelSource.CurrentA;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new OutputChannelRouterEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}

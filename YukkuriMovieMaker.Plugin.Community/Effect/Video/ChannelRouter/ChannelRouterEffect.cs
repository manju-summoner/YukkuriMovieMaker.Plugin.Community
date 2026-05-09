using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ChannelRouter
{
    [VideoEffect(nameof(Texts.ChannelRouterEffectName), [VideoEffectCategories.Filtering], ["channel router", "channel combiner", "set channel", "チャンネルルーター", "通道路由", "채널 라우터", "CustomValue"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class ChannelRouterEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.ChannelRouterEffectName} {TargetIndex}";

        [Display(GroupName = nameof(Texts.ChannelRouterEffectName), Name = nameof(Texts.TargetIndexName), Description = nameof(Texts.TargetIndexDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 1, 16)]
        [Range(1, 1024)]
        [DefaultValue(1)]
        public int TargetIndex
        {
            get => _targetIndex;
            set => Set(ref _targetIndex, Math.Max(1, value), nameof(TargetIndex), nameof(Label));
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
            => new ChannelRouterEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}

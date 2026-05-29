using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    [VideoEffect(nameof(Texts.LuaScript), [VideoEffectCategories.Filtering], ["lua", "script", "スクリプト", "lua script", "アニメーション効果", "animation"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class LuaScriptEffect : VideoEffectBase
    {
        internal const string DefaultScript =
            """
            obj.alpha = math.min(time * 255, 255)
            """;

        public override string Label => Texts.LuaScript;

        [Display(GroupName = nameof(Texts.ScriptGroup), Name = nameof(Texts.ScriptCode), Description = nameof(Texts.ScriptCodeDesc), ResourceType = typeof(Texts))]
        [CodeEditor(Language = "pack://application:,,,/YukkuriMovieMaker.Plugin.Community;component/Resources/SyntaxDefinitions/Lua-{theme}.xshd", FoldingStrategyType = typeof(LuaFoldingStrategy), AutoCompletionStrategyType = typeof(LuaAutoCompletionStrategy), ToolBarStrategyType = typeof(LuaToolBarStrategy), PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public string Script { get => _script; set => Set(ref _script, value); }
        string _script = DefaultScript;

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Track0), Description = nameof(Texts.TrackDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -100, 100)]
        public Animation Track0 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Track1), Description = nameof(Texts.TrackDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -100, 100)]
        public Animation Track1 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Track2), Description = nameof(Texts.TrackDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -100, 100)]
        public Animation Track2 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ParametersGroup), Name = nameof(Texts.Track3), Description = nameof(Texts.TrackDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -100, 100)]
        public Animation Track3 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new LuaScriptEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            Track0, Track1, Track2, Track3,
        ];
    }
}

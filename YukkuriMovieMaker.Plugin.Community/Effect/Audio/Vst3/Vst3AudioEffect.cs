using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    [AudioEffect(nameof(Texts.Vst3EffectName), [nameof(Texts.Vst3EffectCategoryName)], ["VST", "VST3", "プラグイン", "plugin"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class Vst3AudioEffect : AudioEffectBase
    {
        public override string Label => string.IsNullOrEmpty(PluginName) ? Texts.Vst3EffectName : $"{Texts.Vst3EffectName} {PluginName}";

        /// <summary>
        /// VST3モジュール（.vst3）のパス
        /// </summary>
        [Display(GroupName = nameof(Texts.Vst3EffectName), Name = nameof(Texts.Vst3EffectPluginName), Description = nameof(Texts.Vst3EffectPluginDesc), Order = 100, ResourceType = typeof(Texts))]
        [Vst3PluginSelector]
        public string PluginPath { get => pluginPath; set => Set(ref pluginPath, value); }
        string pluginPath = string.Empty;

        /// <summary>
        /// モジュール内のクラスID（TUIDの16進文字列）
        /// </summary>
        public string ClassId { get => classId; set => Set(ref classId, value); }
        string classId = string.Empty;

        public string PluginName { get => pluginName; set => Set(ref pluginName, value, nameof(PluginName), nameof(Label)); }
        string pluginName = string.Empty;

        /// <summary>
        /// プラグインの状態（IComponent::getState）。エディターを閉じたときに更新される
        /// </summary>
        [Display(GroupName = nameof(Texts.Vst3EffectName), Name = nameof(Texts.Vst3EffectOpenEditorName), Description = nameof(Texts.Vst3EffectOpenEditorDesc), Order = 110, ResourceType = typeof(Texts))]
        [Vst3OpenEditorButton]
        public byte[]? ComponentState { get => componentState; set => Set(ref componentState, value); }
        byte[]? componentState;

        /// <summary>
        /// エディットコントローラーの状態（IEditController::getState）
        /// </summary>
        public byte[]? ControllerState { get => controllerState; set => Set(ref controllerState, value); }
        byte[]? controllerState;

        [Display(GroupName = nameof(Texts.Vst3EffectName), Name = nameof(Texts.Vst3EffectMixName), Description = nameof(Texts.Vst3EffectMixDesc), Order = 120, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Mix { get; } = new Animation(100, 0, 100);

        public override Player.Audio.Effects.IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new Vst3AudioEffectProcessor(this);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Mix];
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.DecibelVolume
{
    [AudioEffect(nameof(Texts.DecibelVolumeEffect), [AudioEffectCategories.Basic], ["gain", "volume", "ゲイン", "ボリューム"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class DecibelVolumeEffect : AudioEffectBase
    {
        public override string Label => $"{Texts.DecibelVolumeEffect} {Decibel.GetValue(0, 1, 30):+0.0;-0.0;}dB";

        [Display(GroupName = nameof(Texts.DecibelVolumeEffect), Name = nameof(Texts.DecibelName), Description = nameof(Texts.DecibelDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -10, 10)]
        public Animation Decibel { get; } = new Animation(0, -60, 60);

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new DecibelVolumeEffectProcessor(this);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Decibel.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=音量dB@YMM4-未実装\r\n" +
                $"param=\r\n";
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Decibel];
    }
}

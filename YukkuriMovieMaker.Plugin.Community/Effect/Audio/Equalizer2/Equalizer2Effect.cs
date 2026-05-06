using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    [AudioEffect(nameof(Texts.Equalizer2), [AudioEffectCategories.Filter], ["parametric", "parametriceq", "parametric_eq", "paraeq", "peq", "パラメトリック", "パラメトリックイコライザー", "パライコ"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class Equalizer2Effect : AudioEffectBase
    {
        public override string Label => Texts.Equalizer2;

        [Display(GroupName = nameof(Texts.Equalizer2), Name = nameof(Texts.Empty), ResourceType = typeof(Texts))]
        [BandsEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public ImmutableList<Equalizer2Band> Bands { get => bands; set => Set(ref bands, value); }
        ImmutableList<Equalizer2Band> bands =
        [
            new Equalizer2Band(FilterType.LowShelf, 100, 0, 1.41),
            new Equalizer2Band(FilterType.PeakingEQ, 1000, 0, 1.41),
            new Equalizer2Band(FilterType.HighShelf, 10000, 0, 1.41),
        ];

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new Equalizer2EffectProcessor(this);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => Bands;
    }
}

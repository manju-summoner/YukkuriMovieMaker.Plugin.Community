using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.Bevel
{
    internal enum BevelMode
    {
        [Display(Name = nameof(Texts.StraightBevel), ResourceType = typeof(Texts))]
        Straight = 0,
        [Display(Name = nameof(Texts.RoundBevel), ResourceType = typeof(Texts))]
        Round = 1,
        [Display(Name = nameof(Texts.InvertedRoundBevel), ResourceType = typeof(Texts))]
        InvertedRound = 2,
        [Display(Name = nameof(Texts.StepBevel), ResourceType = typeof(Texts))]
        Step = 3,
        [Display(Name = nameof(Texts.HeadCollerBevel), ResourceType = typeof(Texts))]
        HeadColler = 4,
        [Display(Name = nameof(Texts.StringBevel), ResourceType = typeof(Texts))]
        String = 5,
    }
}

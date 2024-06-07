using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuminanceKey
{
    internal enum LuminanceKeyEffectMode
    {
        [Display(Name = nameof(Texts.LuminanceKeyEffectModeDark), ResourceType = typeof(Texts))]
        Dark,
        [Display(Name = nameof(Texts.LuminanceKeyEffectModeThreshold), ResourceType = typeof(Texts))]
        Threshold,
        [Display(Name = nameof(Texts.LuminanceKeyEffectModeThresholdRange), ResourceType = typeof(Texts))]
        ThresholdRange,
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VignetteBlur
{
    internal enum VignetBlurMode
    {
        [Display(Name = nameof(Texts.GaussianBlur), ResourceType = typeof(Texts))]
        Gaussian = 1,
        [Display(Name = nameof(Texts.RadialBlur), ResourceType = typeof(Texts))]
        Radial = 2,
        [Display(Name = nameof(Texts.CircularBlur), ResourceType = typeof(Texts))]
        Circular = 4,
    }
}

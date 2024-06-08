using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Rainbow
{
    internal enum RainbowColorSpace
    {
        [Display(Name = "HSV")]
        HSV,
        [Display(Name = "LCH")]
        LCH,
    }
}

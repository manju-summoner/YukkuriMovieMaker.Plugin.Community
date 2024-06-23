using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting
{
    internal enum LightingMode
    {
        [Display(Name = nameof(Texts.PointDiffuse), ResourceType = typeof(Texts))]
        PointDiffuse,
        [Display(Name = nameof(Texts.DistantDiffuse), ResourceType = typeof(Texts))]
        DistantDiffuse,
        [Display(Name = nameof(Texts.PointSpecular), ResourceType = typeof(Texts))]
        PointSpecular,
        [Display(Name = nameof(Texts.DistantSpecular), ResourceType = typeof(Texts))]
        DistantSpecular,
    }
}

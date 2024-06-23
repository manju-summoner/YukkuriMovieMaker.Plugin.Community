using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap
{
    internal enum HeightmapMode
    {
        [Display(Name = nameof(Texts.Bevel), ResourceType = typeof(Texts))]
        Bevel,
        [Display(Name = nameof(Texts.Heightmap), ResourceType = typeof(Texts))]
        HeightmapFile,
    }
}

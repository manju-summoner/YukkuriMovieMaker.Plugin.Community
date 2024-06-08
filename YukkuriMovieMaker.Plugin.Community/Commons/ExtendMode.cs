using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.Community.Brush.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Commons
{
    public enum ExtendMode
    {
        [Display(Name = nameof(Texts.ExtendModeClampName), ResourceType = typeof(Texts))]
        Clamp = 0,
        [Display(Name = nameof(Texts.ExtendModeWrapName), ResourceType = typeof(Texts))]
        Wrap = 1,
        [Display(Name = nameof(Texts.ExtendModeMirrorName), ResourceType = typeof(Texts))]
        Mirror = 2,
    }
    internal static class ExtendModeEx
    {
        public static Vortice.Direct2D1.ExtendMode ToD2DExtendMode(this ExtendMode mode)
        {
            return (Vortice.Direct2D1.ExtendMode)mode;
        }
    }
}

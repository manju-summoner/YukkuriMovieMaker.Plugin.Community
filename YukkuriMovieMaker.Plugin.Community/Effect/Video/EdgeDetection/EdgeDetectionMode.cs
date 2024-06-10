using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeDetection
{
    internal enum EdgeDetectionMode
    {
        [Display(Name = "Sobel")]
        Sobel = 0,
        [Display(Name = "Prewitt")]
        Prewitt = 1,
    }
}

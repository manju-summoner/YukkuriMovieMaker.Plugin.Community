using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PixelSort
{
    /// <summary>
    /// ピクセルソートエフェクトの並べ替え方向(明るいピクセルが向かう方向)。
    /// </summary>
    internal enum PixelSortDirection
    {
        [Display(Name = nameof(Texts.PixelSortDirectionDownName), Description = nameof(Texts.PixelSortDirectionDownName), ResourceType = typeof(Texts))]
        Down,
        [Display(Name = nameof(Texts.PixelSortDirectionUpName), Description = nameof(Texts.PixelSortDirectionUpName), ResourceType = typeof(Texts))]
        Up,
        [Display(Name = nameof(Texts.PixelSortDirectionRightName), Description = nameof(Texts.PixelSortDirectionRightName), ResourceType = typeof(Texts))]
        Right,
        [Display(Name = nameof(Texts.PixelSortDirectionLeftName), Description = nameof(Texts.PixelSortDirectionLeftName), ResourceType = typeof(Texts))]
        Left,
    }
}

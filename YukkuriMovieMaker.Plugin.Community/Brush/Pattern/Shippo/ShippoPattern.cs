using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Shippo
{
    public enum ShippoPattern
    {
        [Display(Name = nameof(Texts.ShippoPattern_Basic_Name), Description = nameof(Texts.ShippoPattern_Basic_Description), ResourceType = typeof(Texts))]
        Basic,

        [Display(Name = nameof(Texts.ShippoPattern_DoubleCircle_Name), Description = nameof(Texts.ShippoPattern_DoubleCircle_Description), ResourceType = typeof(Texts))]
        DoubleCircle,

        [Display(Name = nameof(Texts.ShippoPattern_CenterDot_Name), Description = nameof(Texts.ShippoPattern_CenterDot_Description), ResourceType = typeof(Texts))]
        CenterDot,

        [Display(Name = nameof(Texts.ShippoPattern_CornerDot_Name), Description = nameof(Texts.ShippoPattern_CornerDot_Description), ResourceType = typeof(Texts))]
        CornerDot,

        [Display(Name = nameof(Texts.ShippoPattern_Hanabishi_Name), Description = nameof(Texts.ShippoPattern_Hanabishi_Description), ResourceType = typeof(Texts))]
        Hanabishi,
    }
}

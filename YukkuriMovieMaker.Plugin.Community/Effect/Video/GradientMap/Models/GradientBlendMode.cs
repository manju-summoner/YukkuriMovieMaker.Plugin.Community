using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Localization;
using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

public enum GradientBlendMode
{
    [Display(Name = nameof(Texts.BlendModeNormalName),
             Description = nameof(Texts.BlendModeNormalDesc),
             ResourceType = typeof(Texts))]
    Normal = 0,

    [Display(Name = nameof(Texts.BlendModeLuminosityName),
             Description = nameof(Texts.BlendModeLuminosityDesc),
             ResourceType = typeof(Texts))]
    Luminosity = 1,

    [Display(Name = nameof(Texts.BlendModeHueName),
             Description = nameof(Texts.BlendModeHueDesc),
             ResourceType = typeof(Texts))]
    Hue = 2,

    [Display(Name = nameof(Texts.BlendModeDarkenName),
             Description = nameof(Texts.BlendModeDarkenDesc),
             ResourceType = typeof(Texts))]
    Darken = 3,

    [Display(Name = nameof(Texts.BlendModeMultiplyName),
             Description = nameof(Texts.BlendModeMultiplyDesc),
             ResourceType = typeof(Texts))]
    Multiply = 4,

    [Display(Name = nameof(Texts.BlendModeColorBurnName),
             Description = nameof(Texts.BlendModeColorBurnDesc),
             ResourceType = typeof(Texts))]
    ColorBurn = 5,

    [Display(Name = nameof(Texts.BlendModeLinearBurnName),
             Description = nameof(Texts.BlendModeLinearBurnDesc),
             ResourceType = typeof(Texts))]
    LinearBurn = 6,

    [Display(Name = nameof(Texts.BlendModeSubtractName),
             Description = nameof(Texts.BlendModeSubtractDesc),
             ResourceType = typeof(Texts))]
    Subtract = 7,

    [Display(Name = nameof(Texts.BlendModeLightenName),
             Description = nameof(Texts.BlendModeLightenDesc),
             ResourceType = typeof(Texts))]
    Lighten = 8,

    [Display(Name = nameof(Texts.BlendModeScreenName),
             Description = nameof(Texts.BlendModeScreenDesc),
             ResourceType = typeof(Texts))]
    Screen = 9,

    [Display(Name = nameof(Texts.BlendModeColorDodgeName),
             Description = nameof(Texts.BlendModeColorDodgeDesc),
             ResourceType = typeof(Texts))]
    ColorDodge = 10,

    [Display(Name = nameof(Texts.BlendModeLinearDodgeName),
             Description = nameof(Texts.BlendModeLinearDodgeDesc),
             ResourceType = typeof(Texts))]
    LinearDodge = 11,

    [Display(Name = nameof(Texts.BlendModeAddGlowName),
             Description = nameof(Texts.BlendModeAddGlowDesc),
             ResourceType = typeof(Texts))]
    AddGlow = 12,

    [Display(Name = nameof(Texts.BlendModeOverlayName),
             Description = nameof(Texts.BlendModeOverlayDesc),
             ResourceType = typeof(Texts))]
    Overlay = 13,

    [Display(Name = nameof(Texts.BlendModeSoftLightName),
             Description = nameof(Texts.BlendModeSoftLightDesc),
             ResourceType = typeof(Texts))]
    SoftLight = 14,

    [Display(Name = nameof(Texts.BlendModeHardLightName),
             Description = nameof(Texts.BlendModeHardLightDesc),
             ResourceType = typeof(Texts))]
    HardLight = 15,

    [Display(Name = nameof(Texts.BlendModeDifferenceName),
             Description = nameof(Texts.BlendModeDifferenceDesc),
             ResourceType = typeof(Texts))]
    Difference = 16,

    [Display(Name = nameof(Texts.BlendModeVividLightName),
             Description = nameof(Texts.BlendModeVividLightDesc),
             ResourceType = typeof(Texts))]
    VividLight = 17,

    [Display(Name = nameof(Texts.BlendModeLinearLightName),
             Description = nameof(Texts.BlendModeLinearLightDesc),
             ResourceType = typeof(Texts))]
    LinearLight = 18,

    [Display(Name = nameof(Texts.BlendModePinLightName),
             Description = nameof(Texts.BlendModePinLightDesc),
             ResourceType = typeof(Texts))]
    PinLight = 19,

    [Display(Name = nameof(Texts.BlendModeHardMixName),
             Description = nameof(Texts.BlendModeHardMixDesc),
             ResourceType = typeof(Texts))]
    HardMix = 20,

    [Display(Name = nameof(Texts.BlendModeExclusionName),
             Description = nameof(Texts.BlendModeExclusionDesc),
             ResourceType = typeof(Texts))]
    Exclusion = 21,

    [Display(Name = nameof(Texts.BlendModeDarkerColorName),
             Description = nameof(Texts.BlendModeDarkerColorDesc),
             ResourceType = typeof(Texts))]
    DarkerColor = 22,

    [Display(Name = nameof(Texts.BlendModeLighterColorName),
             Description = nameof(Texts.BlendModeLighterColorDesc),
             ResourceType = typeof(Texts))]
    LighterColor = 23,

    [Display(Name = nameof(Texts.BlendModeDivideName),
             Description = nameof(Texts.BlendModeDivideDesc),
             ResourceType = typeof(Texts))]
    Divide = 24,

    [Display(Name = nameof(Texts.BlendModeSaturationName),
             Description = nameof(Texts.BlendModeSaturationDesc),
             ResourceType = typeof(Texts))]
    Saturation = 25,

    [Display(Name = nameof(Texts.BlendModeColorName),
             Description = nameof(Texts.BlendModeColorDesc),
             ResourceType = typeof(Texts))]
    Color = 26,
}

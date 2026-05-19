using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.Shape;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShapePaste
{
    [VideoEffect(nameof(Texts.ShapePasteEffectDefaultName), [VideoEffectCategories.Decoration], ["Shape Paste", "図形貼り付け"], isAviUtlSupported: false, ResourceType = typeof(Texts))]
    internal class ShapePasteEffect : VideoEffectBase
    {
        public override string Label => Texts.ShapePasteEffectDefaultName;

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Draw), Name = nameof(Texts.ShapePasteEffectDisplayName_X), Description = nameof(Texts.ShapePasteEffectDiscription_X), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Pixels), -1000.0, 1000.0, ResourceType = typeof(Texts))]
        [DrawPositionVisible]
        public Animation X { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Draw), Name = nameof(Texts.ShapePasteEffectDisplayName_Y), Description = nameof(Texts.ShapePasteEffectDiscription_Y), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Pixels), -1000.0, 1000.0, ResourceType = typeof(Texts))]
        [DrawPositionVisible]
        public Animation Y { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Draw), Name = nameof(Texts.ShapePasteEffectDisplayName_Z), Description = nameof(Texts.ShapePasteEffectDiscription_Z), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Pixels), -1000.0, 1000.0, ResourceType = typeof(Texts))]
        [DrawPositionVisible]
        public Animation Z { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Draw), Name = nameof(Texts.ShapePasteEffectDisplayName_Opacity), Description = nameof(Texts.ShapePasteEffectDiscription_Opacity), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Percent), 0.0, 100.0, ResourceType = typeof(Texts))]
        public Animation Opacity { get; } = new Animation(100.0, 0.0, 100.0);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Draw), Name = nameof(Texts.ShapePasteEffectDisplayName_Zoom), Description = nameof(Texts.ShapePasteEffectDiscription_Zoom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Percent), 0.0, 200.0, ResourceType = typeof(Texts))]
        [DrawPositionVisible]
        public Animation Zoom { get; } = new Animation(100.0, 0.0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Draw), Name = nameof(Texts.ShapePasteEffectDisplayName_Rotation), Description = nameof(Texts.ShapePasteEffectDiscription_Rotation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Degrees), -360.0, 360.0, ResourceType = typeof(Texts))]
        public Animation Rotation { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Draw), Name = nameof(Texts.ShapePasteEffectDisplayName_InvertX), Description = nameof(Texts.ShapePasteEffectDiscription_InvertX), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool InvertX { get => field; set => Set(ref field, value); }

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Draw), Name = nameof(Texts.ShapePasteEffectDisplayName_InvertY), Description = nameof(Texts.ShapePasteEffectDiscription_InvertY), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool InvertY { get => field; set => Set(ref field, value); }

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Display), Name = nameof(Texts.ShapePasteEffectDisplayName_DisplayMode), Description = nameof(Texts.ShapePasteEffectDiscription_DisplayMode), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ShapeDisplayMode DisplayMode { get => field; set => Set(ref field, value); }
            = ShapeDisplayMode.Overlay;

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Display), Name = nameof(Texts.ShapePasteEffectDisplayName_IsBack), Description = nameof(Texts.ShapePasteEffectDiscription_IsBack), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [IsBackVisible]
        public bool IsBack { get => field; set => Set(ref field, value); }

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_SizeTracking), Name = nameof(Texts.ShapePasteEffectDisplayName_PinLeft), Description = nameof(Texts.ShapePasteEffectDiscription_PinLeft), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool PinLeft { get => field; set => Set(ref field, value, nameof(PinLeft), nameof(IsSizeTrackingEnabled), nameof(IsFullyPinned)); }

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_SizeTracking), Name = nameof(Texts.ShapePasteEffectDisplayName_PinRight), Description = nameof(Texts.ShapePasteEffectDiscription_PinRight), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool PinRight { get => field; set => Set(ref field, value, nameof(PinRight), nameof(IsSizeTrackingEnabled), nameof(IsFullyPinned)); }

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_SizeTracking), Name = nameof(Texts.ShapePasteEffectDisplayName_PinTop), Description = nameof(Texts.ShapePasteEffectDiscription_PinTop), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool PinTop { get => field; set => Set(ref field, value, nameof(PinTop), nameof(IsSizeTrackingEnabled), nameof(IsFullyPinned)); }

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_SizeTracking), Name = nameof(Texts.ShapePasteEffectDisplayName_PinBottom), Description = nameof(Texts.ShapePasteEffectDiscription_PinBottom), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool PinBottom { get => field; set => Set(ref field, value, nameof(PinBottom), nameof(IsSizeTrackingEnabled), nameof(IsFullyPinned)); }

        public bool IsSizeTrackingEnabled => PinLeft || PinRight || PinTop || PinBottom;
        public bool IsFullyPinned => PinLeft && PinRight && PinTop && PinBottom;

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Margin), Name = nameof(Texts.ShapePasteEffectDisplayName_Left), Description = nameof(Texts.ShapePasteEffectDiscription_Left), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Pixels), -500.0, 500.0, ResourceType = typeof(Texts))]
        [LeftMarginVisible]
        public Animation Left { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Margin), Name = nameof(Texts.ShapePasteEffectDisplayName_Right), Description = nameof(Texts.ShapePasteEffectDiscription_Right), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Pixels), -500.0, 500.0, ResourceType = typeof(Texts))]
        [RightMarginVisible]
        public Animation Right { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Margin), Name = nameof(Texts.ShapePasteEffectDisplayName_Top), Description = nameof(Texts.ShapePasteEffectDiscription_Top), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Pixels), -500.0, 500.0, ResourceType = typeof(Texts))]
        [TopMarginVisible]
        public Animation Top { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Margin), Name = nameof(Texts.ShapePasteEffectDisplayName_Bottom), Description = nameof(Texts.ShapePasteEffectDiscription_Bottom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.ShapePasteEffectUnit_Pixels), -500.0, 500.0, ResourceType = typeof(Texts))]
        [BottomMarginVisible]
        public Animation Bottom { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Shape), Name = nameof(Texts.ShapePasteEffectDisplayName_ShapeType), Description = nameof(Texts.ShapePasteEffectDiscription_ShapeType), ResourceType = typeof(Texts))]
        [ShapeTypeComboBox]
        public Type ShapeType { get => field; set => Set(ref field, value); }
            = PluginLoader.GetPrimaryPluginType<IShapePlugin>();

        private Type? oldShapeType;

        [Display(GroupName = nameof(Texts.ShapePasteEffectGroupName_Shape), AutoGenerateField = true, ResourceType = typeof(Texts))]
        public IShapeParameter ShapeParameter { get => field; set => Set(ref field, value); }
            = new RectangleShapeParameter(null);

        public override void BeginEdit()
        {
            oldShapeType = ShapeType;
            base.BeginEdit();
        }

        public override async ValueTask EndEditAsync()
        {
            if (ShapeParameter is null || oldShapeType != ShapeType)
            {
                ShapeParameter = ShapeFactory
                    .GetPlugin(ShapeType)
                    .CreateShapeParameter(ShapeParameter?.GetSharedData());
            }
            await base.EndEditAsync();
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
            => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new ShapePasteEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [X, Y, Z, Opacity, Zoom, Rotation, Left, Right, Top, Bottom, ShapeParameter];
    }
}

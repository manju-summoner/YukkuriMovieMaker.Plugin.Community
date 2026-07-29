using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RotationPerSecond
{
    [VideoEffect(nameof(Texts.rotate_for_seconds), [VideoEffectCategories.Animation,], ["指定秒間回転", "指定秒数回転", "回転", "秒", "指定", "Rotate for Specified Seconds", "Rotate", "Seconds", "Specified"], isAviUtlSupported: false, ResourceType = typeof(Texts))]
    internal class RotationPerSecond : VideoEffectBase
    {
        public override string Label => Texts.rotate_for_seconds;

        [Display(GroupName = nameof(Texts.rotate_for_seconds), Name = nameof(Texts.seconds), Description = nameof(Texts.rotation_period), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.unit_seconds), 0.00, 60, ResourceType = typeof(Texts))]
        public Animation Seconds { get; } = new Animation(1.0, 0.00, 100000000);

        [Display(GroupName = nameof(Texts.rotate_for_seconds), Name = nameof(Texts.axis_x), Description = nameof(Texts.x_rotations), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.unit_times), -100, 100, ResourceType = typeof(Texts))]
        public Animation RotationXCount { get; } = new Animation(0.0, -10000000, 10000000);

        [Display(GroupName = nameof(Texts.rotate_for_seconds), Name = nameof(Texts.axis_y), Description = nameof(Texts.y_rotations), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.unit_times), -100, 100, ResourceType = typeof(Texts))]
        public Animation RotationYCount { get; } = new Animation(0.0, -10000000, 10000000);

        [Display(GroupName = nameof(Texts.rotate_for_seconds), Name = nameof(Texts.axis_z), Description = nameof(Texts.z_rotations), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.unit_times), -100, 100, ResourceType = typeof(Texts))]
        public Animation RotationZCount { get; } = new Animation(0.0, -10000000, 10000000);

        [Display(GroupName = nameof(Texts.rotate_for_seconds), Name = nameof(Texts.x_offset), Description = nameof(Texts.x_initial_angle_offset), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation OffsetX { get; } = new Animation(0.0, -360000000, 360000000);

        [Display(GroupName = nameof(Texts.rotate_for_seconds), Name = nameof(Texts.y_offset), Description = nameof(Texts.y_initial_angle_offset), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation OffsetY { get; } = new Animation(0.0, -360000000, 360000000);

        [Display(GroupName = nameof(Texts.rotate_for_seconds), Name = nameof(Texts.z_offset), Description = nameof(Texts.z_initial_angle_offset), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation OffsetZ { get; } = new Animation(0.0, -360000000, 360000000);

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            Seconds,
            RotationXCount, RotationYCount, RotationZCount,
            OffsetX, OffsetY, OffsetZ,
        ];

        public override IEnumerable<string> CreateExoVideoFilters(
            int keyFrameIndex,
            ExoOutputDescription exoOutputDescription)
            => [];

        public override IVideoEffectProcessor CreateVideoEffect(
            IGraphicsDevicesAndContext devices)
            => new RotationPerSecondProcessor(this);
    }
}
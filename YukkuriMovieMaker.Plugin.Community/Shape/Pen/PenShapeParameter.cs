using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Windows.Ink;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenShapeParameter : ShapeParameterBase
    {
        [Display(Name = nameof(Texts.Pen), Description = nameof(Texts.Pen), ResourceType = typeof(Texts))]
        [OpenPenToolButton]
        public ImmutableList<SerializableStroke> Strokes { get => strokes; set => Set(ref strokes, value); }
        ImmutableList<SerializableStroke> strokes = [];

        [Display(Name = nameof(Texts.Thickness), Description = nameof(Texts.Thickness), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0.1d, 200)]
        public Animation Thickness { get; } = new Animation(100, 0.1, 100000);

        [Display(Name = nameof(Texts.Length), Description = nameof(Texts.Length), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Length { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Texts.Offset), Description = nameof(Texts.Offset), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -100, 100)]
        public Animation Offset { get; } = new Animation(0, -1000000, 1000000);


        public bool IsEditing { get => isEditing; set => Set(ref isEditing, value); }
        bool isEditing = false;

        //JsonSerializer用
        public PenShapeParameter():base() { }

        //通常呼ばれるコンストラクタ。これがないと他の図形を選択した際に値を引き継げない
        public PenShapeParameter(SharedDataStore? sharedData):base(sharedData) { }

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters)
        {
            return [];
        }

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc)
        {
            return [];
        }

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new PenShapeSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Thickness, Length, Offset];

        protected override void LoadSharedData(SharedDataStore store)
        {
            var data = store.Load<SharedData>();
            if (data is null)
                return;
            data.ApplyTo(this);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new SharedData(this));
        }

        class SharedData
        {
            public ImmutableList<SerializableStroke> Strokes { get; set; } = [];

            public Animation Thickness { get; } = new Animation(100, 0.1, 100000);
            public Animation Length { get; } = new Animation(100, 0, 100);
            public Animation Offset { get; } = new Animation(0, -1000000, 1000000);

            public SharedData() { }
            public SharedData(PenShapeParameter parameter)
            {
                Strokes = parameter.Strokes;
                Thickness.CopyFrom(parameter.Thickness);
                Length.CopyFrom(parameter.Length);
                Offset.CopyFrom(parameter.Offset);
            }
            public void ApplyTo(PenShapeParameter parameter)
            {
                parameter.Strokes = Strokes;
                parameter.Thickness.CopyFrom(Thickness);
                parameter.Length.CopyFrom(Length);
                parameter.Offset.CopyFrom(Offset);
            }
        }
    }
}
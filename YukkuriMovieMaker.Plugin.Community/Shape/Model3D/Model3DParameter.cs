using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

internal sealed class Model3DParameter(SharedDataStore? sharedData) : ShapeParameterBase(sharedData), IFileItem, IResourceItem
{
    private string _file = string.Empty;
    private Color _baseColor = Colors.White;
    private ProjectionType _projection = ProjectionType.Perspective;
    private bool _isLightEnabled = true;
    private LightType _lightType = LightType.Point;

    [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.File), ResourceType = typeof(Texts))]
    [Model3DFileSelector]
    public string File
    {
        get => _file;
        set => Set(ref _file, value);
    }

    [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.BaseColor), ResourceType = typeof(Texts))]
    [ColorPicker]
    public Color BaseColor
    {
        get => _baseColor;
        set => Set(ref _baseColor, value);
    }

    [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.Projection), ResourceType = typeof(Texts))]
    [EnumComboBox]
    public ProjectionType Projection
    {
        get => _projection;
        set => Set(ref _projection, value);
    }

    [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.X), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", -1000, 1000)]
    public Animation X { get; } = new(0, -100000, 100000);

    [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Y), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", -1000, 1000)]
    public Animation Y { get; } = new(0, -100000, 100000);

    [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Z), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", -1000, 1000)]
    public Animation Z { get; } = new(0, -100000, 100000);

    [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Fov), ResourceType = typeof(Texts))]
    [AnimationSlider("F0", "°", 1, 179)]
    public Animation Fov { get; } = new(45, 1, 179);

    [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Scale), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 5000)]
    public Animation Scale { get; } = new(100, 0, 100000);

    [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationX), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", -360, 360)]
    public Animation RotationX { get; } = new(0, -36000, 36000);

    [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationY), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", -360, 360)]
    public Animation RotationY { get; } = new(0, -36000, 36000);

    [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationZ), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", -360, 360)]
    public Animation RotationZ { get; } = new(0, -36000, 36000);

    [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.IsLightEnabled), ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool IsLightEnabled
    {
        get => _isLightEnabled;
        set => Set(ref _isLightEnabled, value);
    }

    [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightType), ResourceType = typeof(Texts))]
    [EnumComboBox]
    public LightType LightType
    {
        get => _lightType;
        set => Set(ref _lightType, value);
    }

    [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightX), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", -1000, 1000)]
    public Animation LightX { get; } = new(-150, -100000, 100000);

    [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightY), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", -1000, 1000)]
    public Animation LightY { get; } = new(-250, -100000, 100000);

    [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightZ), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", -1000, 1000)]
    public Animation LightZ { get; } = new(-500, -100000, 100000);

    public Model3DParameter() : this(null) { }

    public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskDesc)
        => [];

    public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc)
        => [];

    public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        => new Model3DSource(devices, this);

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => [X, Y, Z, Fov, Scale, RotationX, RotationY, RotationZ, LightX, LightY, LightZ];

    public IEnumerable<string> GetFiles()
    {
        if (!string.IsNullOrEmpty(File))
            yield return File;
    }

    public void ReplaceFile(string from, string to)
    {
        if (File == from)
            File = to;
    }

    public IEnumerable<TimelineResource> GetResources()
    {
        if (TimelineResource.TryParseFromPath(File, TimelineResourceType.Other, out var resource))
            yield return resource;
    }

    protected override void LoadSharedData(SharedDataStore store)
    {
        var data = store.Load<SharedData>();
        data?.CopyTo(this);
    }

    protected override void SaveSharedData(SharedDataStore store)
        => store.Save(new SharedData(this));

    private sealed class SharedData
    {
        public string File { get; set; } = string.Empty;
        public Color BaseColor { get; set; } = Colors.White;
        public ProjectionType Projection { get; set; } = ProjectionType.Perspective;
        public bool IsLightEnabled { get; set; } = true;
        public LightType LightType { get; set; } = LightType.Point;

        public Animation X { get; } = new(0, -100000, 100000);
        public Animation Y { get; } = new(0, -100000, 100000);
        public Animation Z { get; } = new(0, -100000, 100000);
        public Animation Fov { get; } = new(45, 1, 179);
        public Animation Scale { get; } = new(100, 0, 100000);
        public Animation RotationX { get; } = new(0, -36000, 36000);
        public Animation RotationY { get; } = new(0, -36000, 36000);
        public Animation RotationZ { get; } = new(0, -36000, 36000);
        public Animation LightX { get; } = new(-150, -100000, 100000);
        public Animation LightY { get; } = new(-250, -100000, 100000);
        public Animation LightZ { get; } = new(-500, -100000, 100000);

        public SharedData() { }

        public SharedData(Model3DParameter parameter)
        {
            File = parameter.File;
            BaseColor = parameter.BaseColor;
            Projection = parameter.Projection;
            IsLightEnabled = parameter.IsLightEnabled;
            LightType = parameter.LightType;

            X.CopyFrom(parameter.X);
            Y.CopyFrom(parameter.Y);
            Z.CopyFrom(parameter.Z);
            Fov.CopyFrom(parameter.Fov);
            Scale.CopyFrom(parameter.Scale);
            RotationX.CopyFrom(parameter.RotationX);
            RotationY.CopyFrom(parameter.RotationY);
            RotationZ.CopyFrom(parameter.RotationZ);
            LightX.CopyFrom(parameter.LightX);
            LightY.CopyFrom(parameter.LightY);
            LightZ.CopyFrom(parameter.LightZ);
        }

        public void CopyTo(Model3DParameter parameter)
        {
            parameter.File = File;
            parameter.BaseColor = BaseColor;
            parameter.Projection = Projection;
            parameter.IsLightEnabled = IsLightEnabled;
            parameter.LightType = LightType;

            parameter.X.CopyFrom(X);
            parameter.Y.CopyFrom(Y);
            parameter.Z.CopyFrom(Z);
            parameter.Fov.CopyFrom(Fov);
            parameter.Scale.CopyFrom(Scale);
            parameter.RotationX.CopyFrom(RotationX);
            parameter.RotationY.CopyFrom(RotationY);
            parameter.RotationZ.CopyFrom(RotationZ);
            parameter.LightX.CopyFrom(LightX);
            parameter.LightY.CopyFrom(LightY);
            parameter.LightZ.CopyFrom(LightZ);
        }
    }
}

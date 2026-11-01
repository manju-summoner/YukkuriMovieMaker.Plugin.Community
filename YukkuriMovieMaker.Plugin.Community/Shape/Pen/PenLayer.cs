using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Attributes;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenLayer : Animatable
    {
        const double IndentStep = 12;

        public Guid Id { get; init; } = Guid.NewGuid();

        public string Name { get => name; set => Set(ref name, value); }
        string name = string.Empty;

        public Guid ParentId { get => parentId; set => Set(ref parentId, value); }
        Guid parentId = Guid.Empty;

        public bool IsFolder
        {
            get => isFolder;
            set
            {
                if (Set(ref isFolder, value))
                    OnPropertyChanged(nameof(IsRangeSupported));
            }
        }
        bool isFolder = false;

        [IgnoreUndoRedo]
        public bool IsExpanded { get => isExpanded; set => Set(ref isExpanded, value); }
        bool isExpanded = true;

        public bool IsVisible { get => isVisible; set => Set(ref isVisible, value); }
        bool isVisible = true;

        public bool IsLocked { get => isLocked; set => Set(ref isLocked, value); }
        bool isLocked = false;

        [Display(Name = nameof(Texts.LayerBlendMode), Description = nameof(Texts.LayerBlendMode), GroupName = nameof(Texts.LayerGroup), Order = 100, ResourceType = typeof(Texts))]
        [PenLayerEnumComboBox(nameof(HasLowerLayer))]
        [DefaultValue(Blend.Normal)]
        public Blend BlendMode { get => blendMode; set => Set(ref blendMode, value); }
        Blend blendMode = Blend.Normal;

        public ImmutableList<SerializableStroke> Strokes { get => strokes; set => Set(ref strokes, value); }
        ImmutableList<SerializableStroke> strokes = [];

        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Texts.LayerOpacity), Description = nameof(Texts.LayerOpacity), GroupName = nameof(Texts.LayerGroup), Order = 200, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [DefaultValue(100d)]
        [Range(0d, 100d)]
        [JsonIgnore]
        [IgnoreUndoRedo]
        public double OpacityValue { get => Opacity.Values[0].Value; set => Opacity.Values[0].Value = value; }

        [Display(Name = nameof(Texts.LayerClipping), Description = nameof(Texts.LayerClipping), GroupName = nameof(Texts.LayerGroup), Order = 300, ResourceType = typeof(Texts))]
        [PenLayerToggleSlider(nameof(HasLowerLayer))]
        [DefaultValue(false)]
        public bool IsClipping { get => isClipping; set => Set(ref isClipping, value); }
        bool isClipping = false;

        [Display(Name = nameof(Texts.LayerRangeOverride), Description = nameof(Texts.LayerRangeOverride), GroupName = nameof(Texts.LayerGroup), Order = 400, ResourceType = typeof(Texts))]
        [PenLayerToggleSlider(nameof(IsRangeSupported))]
        [DefaultValue(false)]
        public bool IsRangeOverridden { get => isRangeOverridden; set => Set(ref isRangeOverridden, value); }
        bool isRangeOverridden = false;

        public Animation Length { get; } = new Animation(100, 0, 100);

        [Display(Name = nameof(Texts.Length), Description = nameof(Texts.Length), GroupName = nameof(Texts.LayerGroup), Order = 500, ResourceType = typeof(Texts))]
        [PenLayerSlider("F1", "%", 0, 100, nameof(IsRangeOverridden))]
        [DefaultValue(100d)]
        [Range(0d, 100d)]
        [JsonIgnore]
        [IgnoreUndoRedo]
        public double LengthValue { get => Length.Values[0].Value; set => Length.Values[0].Value = value; }

        public Animation Offset { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Offset), Description = nameof(Texts.Offset), GroupName = nameof(Texts.LayerGroup), Order = 600, ResourceType = typeof(Texts))]
        [PenLayerSlider("F1", "%", -100, 100, nameof(IsRangeOverridden))]
        [DefaultValue(0d)]
        [Range(-100d, 100d)]
        [JsonIgnore]
        [IgnoreUndoRedo]
        public double OffsetValue { get => Offset.Values[0].Value; set => Offset.Values[0].Value = value; }

        [Display(GroupName = nameof(Texts.LayerEffectGroup), Order = 700, ResourceType = typeof(Texts))]
        [VideoEffectSelector]
        public ImmutableList<IVideoEffect> VideoEffects { get => videoEffects; set => Set(ref videoEffects, value); }
        ImmutableList<IVideoEffect> videoEffects = [];

        [JsonIgnore]
        [IgnoreUndoRedo]
        public bool IsRangeSupported => !IsFolder;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public bool HasLowerLayer { get => hasLowerLayer; set => Set(ref hasLowerLayer, value); }
        bool hasLowerLayer;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public System.Windows.Media.ImageSource? Thumbnail { get => thumbnail; set => Set(ref thumbnail, value); }
        System.Windows.Media.ImageSource? thumbnail;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public int Depth
        {
            get => depth;
            set
            {
                if (Set(ref depth, value))
                    OnPropertyChanged(nameof(IndentWidth));
            }
        }
        int depth;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public double IndentWidth => depth * IndentStep;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public bool IsRenaming { get => isRenaming; set => Set(ref isRenaming, value); }
        bool isRenaming;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public string EditName { get => editName; set => Set(ref editName, value); }
        string editName = string.Empty;

        public PenLayer()
        {
            WatchValue(Opacity, nameof(OpacityValue));
            WatchValue(Length, nameof(LengthValue));
            WatchValue(Offset, nameof(OffsetValue));
        }

        void WatchValue(Animation animation, string name)
        {
            var current = animation.Values[0];
            current.PropertyChanged += OnValueChanged;
            animation.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(Animation.Values) || ReferenceEquals(current, animation.Values[0]))
                    return;

                current.PropertyChanged -= OnValueChanged;
                current = animation.Values[0];
                current.PropertyChanged += OnValueChanged;
                OnPropertyChanged(name);
            };

            void OnValueChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(name);
        }

        public void BeginRename()
        {
            EditName = Name;
            IsRenaming = true;
        }

        public void CommitRename()
        {
            if (!IsRenaming)
                return;

            IsRenaming = false;
            if (!string.IsNullOrWhiteSpace(EditName))
                Name = EditName;
        }

        public void CancelRename()
        {
            IsRenaming = false;
        }

        public PenLayer Clone(Guid id)
        {
            var layer = new PenLayer
            {
                Id = id,
                ParentId = ParentId,
                IsFolder = IsFolder,
                IsExpanded = IsExpanded,
                Name = Name,
                IsVisible = IsVisible,
                IsLocked = IsLocked,
                BlendMode = BlendMode,
                IsClipping = IsClipping,
                IsRangeOverridden = IsRangeOverridden,
                Strokes = Strokes,
                VideoEffects = CloneEffects(VideoEffects),
            };
            layer.Opacity.CopyFrom(Opacity);
            layer.Length.CopyFrom(Length);
            layer.Offset.CopyFrom(Offset);
            return layer;
        }

        static ImmutableList<IVideoEffect> CloneEffects(ImmutableList<IVideoEffect> effects)
            => effects.IsEmpty ? [] : YukkuriMovieMaker.Json.Json.GetClone(effects) ?? [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, Length, Offset, .. videoEffects];
    }
}

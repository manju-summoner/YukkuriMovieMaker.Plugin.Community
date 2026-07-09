using System.Collections.Immutable;
using System.ComponentModel;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Fog
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class DepthSettingsVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(DepthVisibilitySource.HasDepth))
            {
                Source = new DepthVisibilitySource(((FogEffect)item).DepthAmount),
                Converter = new DepthSettingsVisibleConverter()
            };
        }

        sealed class DepthVisibilitySource : INotifyPropertyChanged
        {
            readonly Animation _animation;
            ImmutableList<AnimationValue> _subscribedValues = ImmutableList<AnimationValue>.Empty;

            public event PropertyChangedEventHandler? PropertyChanged;

            public bool HasDepth => _animation.Values.Any(v => v.Value > 0);

            public DepthVisibilitySource(Animation animation)
            {
                _animation = animation;
                PropertyChangedEventManager.AddHandler(animation, Animation_PropertyChanged, string.Empty);
                Subscribe();
            }

            void Subscribe()
            {
                _subscribedValues = _animation.Values;
                foreach (var value in _subscribedValues)
                    PropertyChangedEventManager.AddHandler(value, Value_PropertyChanged, nameof(AnimationValue.Value));
            }

            void Unsubscribe()
            {
                foreach (var value in _subscribedValues)
                    PropertyChangedEventManager.RemoveHandler(value, Value_PropertyChanged, nameof(AnimationValue.Value));
                _subscribedValues = ImmutableList<AnimationValue>.Empty;
            }

            void Animation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(Animation.Values) && e.PropertyName != nameof(Animation.AnimationType))
                    return;
                Unsubscribe();
                Subscribe();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasDepth)));
            }

            void Value_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasDepth)));
            }
        }
    }
}

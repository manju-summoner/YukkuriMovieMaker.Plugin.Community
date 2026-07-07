using System;
using System.Collections.Generic;
using System.ComponentModel;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointItemViewModel : Bindable, IDisposable
    {
        bool disposedValue;

        public VectorFieldPoint Model { get; }

        public bool IsEnabled
        {
            get => Model.IsEnabled;
            set => Model.IsEnabled = value;
        }

        public bool IsSelected
        {
            get => Model.IsSelected;
            set => Model.IsSelected = value;
        }

        public event EventHandler? VisualChanged;

        IEnumerable<Animation> VisualAnimations => [Model.X, Model.Y, Model.RadialStrength, Model.VortexStrength, Model.Radius];

        public VectorFieldPointItemViewModel(VectorFieldPoint model)
        {
            Model = model;

            SubscribeValues();
            foreach (var animation in VisualAnimations)
                animation.PropertyChanged += Animation_PropertyChanged;
            Model.PropertyChanged += Model_PropertyChanged;
        }

        void SubscribeValues()
        {
            foreach (var animation in VisualAnimations)
                foreach (var value in animation.Values)
                    value.PropertyChanged += Value_PropertyChanged;
        }

        void UnsubscribeValues()
        {
            foreach (var animation in VisualAnimations)
                foreach (var value in animation.Values)
                    value.PropertyChanged -= Value_PropertyChanged;
        }

        void Animation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Animation.Values) && e.PropertyName != nameof(Animation.AnimationType))
                return;
            UnsubscribeValues();
            SubscribeValues();
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }

        void Value_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }

        void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(VectorFieldPoint.IsEnabled):
                    OnPropertyChanged(nameof(IsEnabled));
                    break;
                case nameof(VectorFieldPoint.IsSelected):
                    OnPropertyChanged(nameof(IsSelected));
                    break;
            }
        }

        public void Dispose()
        {
            if (disposedValue)
                return;
            UnsubscribeValues();
            foreach (var animation in VisualAnimations)
                animation.PropertyChanged -= Animation_PropertyChanged;
            Model.PropertyChanged -= Model_PropertyChanged;
            disposedValue = true;
            GC.SuppressFinalize(this);
        }
    }
}

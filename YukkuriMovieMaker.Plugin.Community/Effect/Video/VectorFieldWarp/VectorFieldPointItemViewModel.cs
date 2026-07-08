using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointItemViewModel : Bindable, IDisposable
    {
        readonly Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
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
            RaiseVisualChanged();
        }

        void Value_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaiseVisualChanged();
        }

        void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(VectorFieldPoint.IsEnabled):
                    RaisePropertyChanged(nameof(IsEnabled));
                    break;
                case nameof(VectorFieldPoint.IsSelected):
                    RaisePropertyChanged(nameof(IsSelected));
                    break;
            }
        }

        void RaiseVisualChanged()
        {
            if (dispatcher.CheckAccess())
                VisualChanged?.Invoke(this, EventArgs.Empty);
            else
                dispatcher.BeginInvoke(RaiseVisualChanged);
        }

        void RaisePropertyChanged(string propertyName)
        {
            if (dispatcher.CheckAccess())
                OnPropertyChanged(propertyName);
            else
                dispatcher.BeginInvoke(() => OnPropertyChanged(propertyName));
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

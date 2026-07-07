using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointItemViewModel : Bindable, IDisposable
    {
        bool disposedValue;

        public VectorFieldPoint Model { get; }
        public string Label { get; }

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

        public ICommand SelectCommand { get; }

        public event EventHandler? PositionChanged;

        public VectorFieldPointItemViewModel(VectorFieldPoint model, int index, ICommand selectCommand)
        {
            Model = model;
            Label = $"#{index + 1}";
            SelectCommand = selectCommand;

            SubscribeValues();
            Model.X.PropertyChanged += Animation_PropertyChanged;
            Model.Y.PropertyChanged += Animation_PropertyChanged;
            Model.PropertyChanged += Model_PropertyChanged;
        }

        void SubscribeValues()
        {
            foreach (var v in Model.X.Values) v.PropertyChanged += Position_PropertyChanged;
            foreach (var v in Model.Y.Values) v.PropertyChanged += Position_PropertyChanged;
        }

        void UnsubscribeValues()
        {
            foreach (var v in Model.X.Values) v.PropertyChanged -= Position_PropertyChanged;
            foreach (var v in Model.Y.Values) v.PropertyChanged -= Position_PropertyChanged;
        }

        void Animation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Animation.Values) && e.PropertyName != nameof(Animation.AnimationType))
                return;
            UnsubscribeValues();
            SubscribeValues();
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        void Position_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PositionChanged?.Invoke(this, EventArgs.Empty);
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
            Model.X.PropertyChanged -= Animation_PropertyChanged;
            Model.Y.PropertyChanged -= Animation_PropertyChanged;
            Model.PropertyChanged -= Model_PropertyChanged;
            disposedValue = true;
            GC.SuppressFinalize(this);
        }
    }
}

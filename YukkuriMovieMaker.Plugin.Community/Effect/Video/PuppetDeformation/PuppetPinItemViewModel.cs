using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    internal sealed class PuppetPinItemViewModel : Bindable, IDisposable
    {
        bool disposedValue;

        public PuppetPin Model { get; }

        public string Position => $"{(Model.RestX.Values.FirstOrDefault()?.Value ?? 0):F0}px, {(Model.RestY.Values.FirstOrDefault()?.Value ?? 0):F0}px";

        public bool IsRestSelected
        {
            get => Model.IsRestSelected;
            set => Model.IsRestSelected = value;
        }

        public bool IsOffsetSelected
        {
            get => Model.IsOffsetSelected;
            set => Model.IsOffsetSelected = value;
        }

        public bool IsEnabled
        {
            get => Model.IsEnabled;
            set => Model.IsEnabled = value;
        }

        public double OffsetAngle
        {
            get
            {
                if (Model.OffsetX.Values.Count == 0 || Model.OffsetY.Values.Count == 0) return 0;
                var ox = Model.OffsetX.Values.First().Value;
                var oy = Model.OffsetY.Values.First().Value;
                return Math.Atan2(oy, ox) * 180.0 / Math.PI;
            }
        }

        public bool IsOffsetZero
        {
            get
            {
                if (Model.OffsetX.Values.Count == 0 || Model.OffsetY.Values.Count == 0) return true;
                return Math.Abs(Model.OffsetX.Values.First().Value) < 0.1
                    && Math.Abs(Model.OffsetY.Values.First().Value) < 0.1;
            }
        }

        public ICommand SelectRestCommand { get; }
        public ICommand SelectOffsetCommand { get; }

        public event EventHandler? OffsetChanged;
        public event EventHandler? RestChanged;

        public PuppetPinItemViewModel(PuppetPin model, ICommand selectRestCommand, ICommand selectOffsetCommand)
        {
            Model = model;
            SelectRestCommand = selectRestCommand;
            SelectOffsetCommand = selectOffsetCommand;

            SubscribeValues();
            Model.OffsetX.PropertyChanged += Animation_PropertyChanged;
            Model.OffsetY.PropertyChanged += Animation_PropertyChanged;
            Model.RestX.PropertyChanged += Animation_PropertyChanged;
            Model.RestY.PropertyChanged += Animation_PropertyChanged;

            Model.PropertyChanged += Model_PropertyChanged;
        }

        void SubscribeValues()
        {
            foreach (var v in Model.OffsetX.Values) v.PropertyChanged += Offset_PropertyChanged;
            foreach (var v in Model.OffsetY.Values) v.PropertyChanged += Offset_PropertyChanged;
            foreach (var v in Model.RestX.Values) v.PropertyChanged += Rest_PropertyChanged;
            foreach (var v in Model.RestY.Values) v.PropertyChanged += Rest_PropertyChanged;
        }

        void UnsubscribeValues()
        {
            foreach (var v in Model.OffsetX.Values) v.PropertyChanged -= Offset_PropertyChanged;
            foreach (var v in Model.OffsetY.Values) v.PropertyChanged -= Offset_PropertyChanged;
            foreach (var v in Model.RestX.Values) v.PropertyChanged -= Rest_PropertyChanged;
            foreach (var v in Model.RestY.Values) v.PropertyChanged -= Rest_PropertyChanged;
        }

        void Animation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Animation.Values) && e.PropertyName != nameof(Animation.AnimationType))
                return;
            UnsubscribeValues();
            SubscribeValues();
            OnPropertyChanged(nameof(OffsetAngle));
            OnPropertyChanged(nameof(IsOffsetZero));
        }

        void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PuppetPin.IsRestSelected):
                    OnPropertyChanged(nameof(IsRestSelected));
                    break;
                case nameof(PuppetPin.IsOffsetSelected):
                    OnPropertyChanged(nameof(IsOffsetSelected));
                    break;
                case nameof(PuppetPin.IsEnabled):
                    OnPropertyChanged(nameof(IsEnabled));
                    break;
            }
        }

        void Offset_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(OffsetAngle));
            OnPropertyChanged(nameof(IsOffsetZero));
            OffsetChanged?.Invoke(this, EventArgs.Empty);
        }

        void Rest_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Position));
            RestChanged?.Invoke(this, EventArgs.Empty);
        }

        void Dispose(bool disposing)
        {
            if (disposedValue) return;
            if (disposing)
            {
                UnsubscribeValues();
                Model.OffsetX.PropertyChanged -= Animation_PropertyChanged;
                Model.OffsetY.PropertyChanged -= Animation_PropertyChanged;
                Model.RestX.PropertyChanged -= Animation_PropertyChanged;
                Model.RestY.PropertyChanged -= Animation_PropertyChanged;
                Model.PropertyChanged -= Model_PropertyChanged;
            }
            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

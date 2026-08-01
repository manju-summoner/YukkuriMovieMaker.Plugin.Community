using System;
using System.ComponentModel;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    /// <summary>ピン配置キャンバスに表示するボーンのビューモデル</summary>
    internal sealed class PuppetBoneViewModel : Bindable, IDisposable
    {
        bool disposedValue;

        public PuppetBone Model { get; }

        public bool IsSelected
        {
            get => Model.IsSelected;
            set => Model.IsSelected = value;
        }

        public bool IsEnabled
        {
            get => Model.IsEnabled;
            set => Model.IsEnabled = value;
        }

        /// <summary>ジョイント位置・親子関係などキャンバス表示に影響する変更</summary>
        public event EventHandler? VisualChanged;

        public PuppetBoneViewModel(PuppetBone model)
        {
            Model = model;

            SubscribeValues();
            Model.JointX.PropertyChanged += Animation_PropertyChanged;
            Model.JointY.PropertyChanged += Animation_PropertyChanged;
            Model.PropertyChanged += Model_PropertyChanged;
        }

        void SubscribeValues()
        {
            foreach (var v in Model.JointX.Values) v.PropertyChanged += Joint_PropertyChanged;
            foreach (var v in Model.JointY.Values) v.PropertyChanged += Joint_PropertyChanged;
        }

        void UnsubscribeValues()
        {
            foreach (var v in Model.JointX.Values) v.PropertyChanged -= Joint_PropertyChanged;
            foreach (var v in Model.JointY.Values) v.PropertyChanged -= Joint_PropertyChanged;
        }

        void Animation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Animation.Values) && e.PropertyName != nameof(Animation.AnimationType))
                return;
            UnsubscribeValues();
            SubscribeValues();
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }

        void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PuppetBone.IsSelected):
                    OnPropertyChanged(nameof(IsSelected));
                    break;
                case nameof(PuppetBone.IsEnabled):
                    OnPropertyChanged(nameof(IsEnabled));
                    VisualChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case nameof(PuppetBone.ParentId):
                    VisualChanged?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        void Joint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            VisualChanged?.Invoke(this, EventArgs.Empty);
        }

        void Dispose(bool disposing)
        {
            if (disposedValue) return;
            if (disposing)
            {
                UnsubscribeValues();
                Model.JointX.PropertyChanged -= Animation_PropertyChanged;
                Model.JointY.PropertyChanged -= Animation_PropertyChanged;
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

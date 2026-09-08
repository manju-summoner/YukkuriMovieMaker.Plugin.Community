using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    /// <summary>
    /// 再生開始位置の編集欄を、参照元が時間を持つときだけ表示する。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class ColorTransferTimeOffsetVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            if (propertyOwner is not ColorTransferEffect effect)
                throw new ArgumentException($"{nameof(ColorTransferEffect)} ではありません", nameof(propertyOwner));

            return new Binding(nameof(VisibilitySource.Visibility)) { Source = new VisibilitySource(effect) };
        }

        public static bool IsTimeOffsetAvailable(ColorTransferReference reference, string filePath)
            => reference switch
            {
                ColorTransferReference.Timeline or ColorTransferReference.Scene => true,
                ColorTransferReference.File => ColorTransferFileKind.IsVideo(filePath),
                _ => false,
            };

        /// <summary>
        /// 可視性のバインディングは1本しか返せないため、参照元とファイルの2つの変更をまとめて1つの値に畳む。
        /// アイテムの購読は弱いイベントにして、編集欄が閉じたあともアイテム側に残らないようにする。
        /// </summary>
        internal sealed class VisibilitySource : INotifyPropertyChanged, IWeakEventListener
        {
            private readonly ColorTransferEffect _effect;

            public VisibilitySource(ColorTransferEffect effect)
            {
                _effect = effect;
                PropertyChangedEventManager.AddListener(effect, this, string.Empty);
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public Visibility Visibility
                => IsTimeOffsetAvailable(_effect.Reference, _effect.FilePath) ? Visibility.Visible : Visibility.Collapsed;

            public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
            {
                if (e is PropertyChangedEventArgs args
                    && (string.IsNullOrEmpty(args.PropertyName)
                        || args.PropertyName is nameof(ColorTransferEffect.Reference) or nameof(ColorTransferEffect.FilePath)))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visibility)));
                }
                return true;
            }
        }
    }
}

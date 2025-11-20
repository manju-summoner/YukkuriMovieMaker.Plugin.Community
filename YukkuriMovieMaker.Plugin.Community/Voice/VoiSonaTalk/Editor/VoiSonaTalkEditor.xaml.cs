using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    /// <summary>
    /// VoiSonaTalkEditor.xaml の相互作用ロジック
    /// </summary>
    public partial class VoiSonaTalkEditor : UserControl, IPropertyEditorControl2
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;
        IEditorInfo? info;

        public VoiSonaTalkVoicePronounce? Pronounce
        {
            get { return (VoiSonaTalkVoicePronounce?)GetValue(PronounceProperty); }
            set { SetValue(PronounceProperty, value); }
        }
        public static readonly DependencyProperty PronounceProperty =
            DependencyProperty.Register(nameof(Pronounce), typeof(VoiSonaTalkVoicePronounce), typeof(VoiSonaTalkEditor), new PropertyMetadata(null, OnPronounceChanged));

        private static void OnPronounceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if(d is not VoiSonaTalkEditor editor)
                return;
            editor.OnPronounceChanged();
        }

        public VoiSonaTalkEditor()
        {
            InitializeComponent();
        }

        private void PopupButton_BeginEdit(object sender, EventArgs e)
        {
            BeginEdit?.Invoke(this, e);
        }

        private void PopupButton_EndEdit(object sender, EventArgs e)
        {
            if (DataContext is VoiSonaTalkEditorViewModel vm)
                vm.StopPlayback();
            EndEdit?.Invoke(this, e);
        }

        public void SetEditorInfo(IEditorInfo info)
        {
            this.info = info;
            if (DataContext is VoiSonaTalkEditorViewModel vm)
                vm.Info = info;
        }

        private void OnPronounceChanged()
        {
            UpdateViewModel();
        }
        void UpdateViewModel()
        {
            if (DataContext is VoiSonaTalkEditorViewModel vm)
                vm.StopPlayback();

            if (Pronounce is not null)
            {
                DataContext = new VoiSonaTalkEditorViewModel(Pronounce)
                {
                    Info = info
                };
            }
            else
            {
                DataContext = null;
            }
        }
    }
}

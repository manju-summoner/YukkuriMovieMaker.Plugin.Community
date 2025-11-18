using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    /// <summary>
    /// VoiSonaTalkEditor.xaml の相互作用ロジック
    /// </summary>
    public partial class VoiSonaTalkEditor : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public VoiSonaTalkVoicePronounce? Pronounce
        {
            get { return (VoiSonaTalkVoicePronounce?)GetValue(PronounceProperty); }
            set { SetValue(PronounceProperty, value); }
        }
        public static readonly DependencyProperty PronounceProperty =
            DependencyProperty.Register(nameof(Pronounce), typeof(VoiSonaTalkVoicePronounce), typeof(VoiSonaTalkEditor), new PropertyMetadata(null));

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
            EndEdit?.Invoke(this, e);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    /// <summary>
    /// KotodamaSettingsView.xaml の相互作用ロジック
    /// </summary>
    public partial class KotodamaSettingsView : UserControl
    {
        public KotodamaSettingsView()
        {
            InitializeComponent();
        }

        private void AddSpeakerButton_Click(object sender, RoutedEventArgs e)
        {
            var builder = KotodamaSettings.Default.Speakers.ToBuilder();
            var newSpeaker = new KotodamaSpeakerSettings()
            {
                Name = Texts.NewCharacter,
                SpeakerId = string.Empty,
                Decorations = [..KotodamaDefaultDecoration.CharacterDefaultDecorations.Select(x => new KotodamaDecorationSettings(x))],
            };
            var insertIndex = Math.Clamp(SpeakersListBox.SelectedIndex + 1, 0, builder.Count);
            builder.Insert(insertIndex, newSpeaker);
            KotodamaSettings.Default.Speakers = builder.ToImmutable();
            SpeakersListBox.SelectedIndex = insertIndex;
        }

        private void DeleteSpeakerButton_Click_1(object sender, RoutedEventArgs e)
        {
            var builder = KotodamaSettings.Default.Speakers.ToBuilder();
            var selectedIndex = SpeakersListBox.SelectedIndex;
            if(selectedIndex < 0 || selectedIndex >= builder.Count)
                return;
            builder.RemoveAt(selectedIndex);
            KotodamaSettings.Default.Speakers = builder.ToImmutable();
            if (builder.Count > 0)
                SpeakersListBox.SelectedIndex = Math.Clamp(selectedIndex - 1, 0, builder.Count - 1);
        }

        private void AddDecorationButton_Click_2(object sender, RoutedEventArgs e)
        {
            if (SpeakersListBox.SelectedItem is not KotodamaSpeakerSettings speaker)
                return;
            if (FindChild<ListBox>(SpeakerContentControl, "DecorationListBox") is not ListBox decorationListBox)
                return;

            var newDecoration = new KotodamaDecorationSettings()
            {
                Name = Texts.NewDecoration,
                DecorationId = string.Empty,
            };

            var decorationBuilder = speaker.Decorations.ToBuilder();
            var insertIndex = Math.Clamp(decorationListBox.SelectedIndex + 1, 0, decorationBuilder.Count);
            decorationBuilder.Insert(insertIndex, newDecoration);
            speaker.Decorations = decorationBuilder.ToImmutable();
            decorationListBox.SelectedIndex = insertIndex;
        }

        private void DeleteDecorationButton_Click_3(object sender, RoutedEventArgs e)
        {
            if (SpeakersListBox.SelectedItem is not KotodamaSpeakerSettings speaker)
                return;
            if (FindChild<ListBox>(SpeakerContentControl, "DecorationListBox") is not ListBox decorationListBox)
                return;
            var decorationBuilder = speaker.Decorations.ToBuilder();
            var selectedIndex = decorationListBox.SelectedIndex;
            if(selectedIndex < 0 || selectedIndex >= decorationBuilder.Count)
                return;
            decorationBuilder.RemoveAt(selectedIndex);
            speaker.Decorations = decorationBuilder.ToImmutable();
            if (decorationBuilder.Count > 0)
                decorationListBox.SelectedIndex = Math.Clamp(selectedIndex - 1, 0, decorationBuilder.Count - 1);
        }
        public static T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t)
                {
                    if (string.IsNullOrEmpty(childName))
                        return t;

                    if (child is FrameworkElement fe && fe.Name == childName)
                        return t;
                }

                var result = FindChild<T>(child, childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void SpeakersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FindChild<ListBox>(SpeakerContentControl, "DecorationListBox") is not ListBox decorationListBox)
                return;
            if (decorationListBox.Items.Count > 0 && decorationListBox.SelectedIndex is -1)
                decorationListBox.SelectedIndex = 0;
        }
    }
}

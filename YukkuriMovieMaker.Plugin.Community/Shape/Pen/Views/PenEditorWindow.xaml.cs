using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    public partial class PenEditorWindow : Window
    {
        public PenEditorWindow()
        {
            InitializeComponent();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Handled || e.OriginalSource is TextBoxBase || Keyboard.Modifiers is not ModifierKeys.None || DataContext is not PenEditorViewModel viewModel)
                return;

            var command = e.Key switch
            {
                Key.D1 or Key.NumPad1 => viewModel.SelectPenCommand,
                Key.D2 or Key.NumPad2 => viewModel.SelectPencilCommand,
                Key.D3 or Key.NumPad3 => viewModel.SelectHighlighterCommand,
                Key.D4 or Key.NumPad4 => viewModel.SelectEraserCommand,
                Key.D5 or Key.NumPad5 => viewModel.SelectFillCommand,
                Key.D6 or Key.NumPad6 => viewModel.SelectSelectionCommand,
                Key.D7 or Key.NumPad7 => viewModel.SelectOrderCommand,
                _ => null,
            };
            if (command is null || !command.CanExecute(null))
                return;

            command.Execute(null);
            e.Handled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

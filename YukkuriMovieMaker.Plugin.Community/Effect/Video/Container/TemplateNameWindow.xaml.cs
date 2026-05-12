using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public partial class TemplateNameWindow : Window
{
    public string TemplateName => TemplateNameTextBox.Text;
    public TemplateWindowResult Result { get; private set; } = TemplateWindowResult.None;

    public TemplateNameWindow(string defaultName, bool isEditMode)
    {
        InitializeComponent();
        TemplateNameTextBox.Text = defaultName;
        Title = Texts.Menu_Template;

        if (isEditMode)
        {
            LeftButton.Content = Texts.Menu_Complete;
            RightButton.Content = Texts.Menu_Delete;

            LeftButton.Click += (s, e) => { Result = TemplateWindowResult.Complete; DialogResult = true; Close(); };
            RightButton.Click += (s, e) => { Result = TemplateWindowResult.Delete; DialogResult = false; Close(); };
        }
        else
        {
            LeftButton.Content = Texts.Menu_Create;
            RightButton.Content = Texts.Menu_Cancel;
            RightButton.IsCancel = true;

            LeftButton.Click += (s, e) => { Result = TemplateWindowResult.Create; DialogResult = true; Close(); };
            RightButton.Click += (s, e) => { Result = TemplateWindowResult.Cancel; DialogResult = false; Close(); };
        }

        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                LeftButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                e.Handled = true;
                return;
            }

            if (isEditMode && e.Key == System.Windows.Input.Key.Escape)
            {
                Result = TemplateWindowResult.None;
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        };
    }
}

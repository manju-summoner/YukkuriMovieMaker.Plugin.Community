using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public partial class BookmarkNameWindow : Window
{
    public string BookmarkName => BookmarkNameTextBox.Text;
    public BookmarkWindowResult Result { get; private set; } = BookmarkWindowResult.None;

    public BookmarkNameWindow(string defaultName, bool isEditMode)
    {
        InitializeComponent();
        BookmarkNameTextBox.Text = defaultName;
        Title = Texts.Menu_Bookmark;

        if (isEditMode)
        {
            LeftButton.Content = Texts.Menu_Complete;
            RightButton.Content = Texts.Menu_Delete;

            LeftButton.Click += (s, e) => { Result = BookmarkWindowResult.Complete; DialogResult = true; Close(); };
            RightButton.Click += (s, e) => { Result = BookmarkWindowResult.Delete; DialogResult = false; Close(); };
        }
        else
        {
            LeftButton.Content = Texts.Menu_Create;
            RightButton.Content = Texts.Menu_Cancel;

            LeftButton.Click += (s, e) => { Result = BookmarkWindowResult.Create; DialogResult = true; Close(); };
            RightButton.Click += (s, e) => { Result = BookmarkWindowResult.Cancel; DialogResult = false; Close(); };
        }
    }
}

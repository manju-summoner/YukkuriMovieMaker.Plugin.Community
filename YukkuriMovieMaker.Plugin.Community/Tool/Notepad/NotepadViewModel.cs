using System.IO;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.ViewModels;
using YukkuriMovieMaker.Views.Behaviors;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class NotepadViewModel : Bindable, IToolViewModel
    {
        public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;

        public string Title => string.IsNullOrEmpty(FileName) ? Texts.Notepad : $"{FileName}{(IsSaved ? string.Empty : "*")}";
        public string FileName => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetFileNameWithoutExtension(FilePath);
        public string FilePath { get; set => Set(ref field, value, nameof(FilePath), nameof(FileName), nameof(Title)); } = string.Empty;
        public bool IsSaved { get; set => Set(ref field, value, nameof(IsSaved), nameof(Title)); } = true;
        public string Text
        {
            get;
            set
            {
                if (Set(ref field, value))
                    IsSaved = false;
            }
        } = string.Empty;
        public double FontSize => Math.Max(8, SystemFonts.MessageFontSize * Zoom);
        public double Zoom { get; set => Set(ref field, Math.Max(0.25, value), nameof(Zoom), nameof(FontSize)); } = 1.0;
        public bool WordWrap { get; set => Set(ref field, value); } = false;
        public bool ShowLineNumbers { get; set => Set(ref field, value); } = false;

        public OpenFileDialogViewModel OpenFileDialog { get; } = new();
        public SaveFileDialogViewModel SaveFileDialog { get; } = new();

        public ICommand ChangeFontSizeCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand OpenNewTabCommand { get; }
        public ICommand InsertImageCommand { get; }

        public event EventHandler<NotepadImageInsertRequestedEventArgs>? ImageInsertRequested;

        public NotepadViewModel()
        {
            ChangeFontSizeCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    if (x is not MouseWheelEventArgs args || Keyboard.Modifiers is not ModifierKeys.Control)
                        return;
                    Zoom += 0.25 * Math.Sign(args.Delta);
                    args.Handled = true;
                });
            OpenCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var filePath = OpenFileDialog.Show(BuildOpenFilter());
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                        return;
                    try
                    {
                        var (text, _) = NotepadDocumentSerializer.Load(filePath);
                        Text = text;
                        FilePath = filePath;
                        IsSaved = true;
                    }
                    catch (Exception ex)
                    {
                        var message = $"{Texts.FailedToOpenFile}\r\n{ex.Message}";
                        Log.Default.Write(message, ex);
                        MessageBox.Show(message, Texts.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            SaveCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    var filePath = x as string;
                    var preferredExt = NotepadDocumentSerializer.DetermineSaveExtension(Text);

                    if (string.IsNullOrEmpty(filePath) || !MatchesPreferredExtension(filePath, preferredExt))
                    {
                        filePath = SaveFileDialog.Show(BuildSaveFilter(preferredExt), BuildDefaultFileName(preferredExt));
                        if (string.IsNullOrEmpty(filePath))
                            return;
                        filePath = EnsureExtension(filePath, preferredExt);
                        FilePath = filePath;
                    }

                    try
                    {
                        NotepadDocumentSerializer.Save(filePath, Text);
                        IsSaved = true;
                    }
                    catch (Exception ex)
                    {
                        var message = $"{Texts.FailedToSaveFile}\r\n{ex.Message}";
                        Log.Default.Write(message, ex);
                        MessageBox.Show(message, Texts.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            OpenNewTabCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var notepadState = new NotepadState()
                    {
                        Zoom = Zoom,
                        WordWrap = WordWrap,
                        ShowLineNumbers = ShowLineNumbers
                    };
                    var toolState = new ToolState()
                    {
                        SavedState = Json.Json.GetJsonText(notepadState)
                    };
                    CreateNewToolViewRequested?.Invoke(this, new CreateNewToolViewRequestedEventArgs(toolState));
                });
            InsertImageCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var filePath = OpenFileDialog.Show($"{Texts.ImageFile}|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.tiff;*.tif");
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                        return;
                    ImageInsertRequested?.Invoke(this, new NotepadImageInsertRequestedEventArgs(filePath));
                });
        }

        public void LoadState(ToolState stateData)
        {
            if (stateData.SavedState is null)
                return;
            var state = Json.Json.LoadFromText<NotepadState>(stateData.SavedState);
            if (state is null)
                return;
            FilePath = state.FilePath;
            Text = state.Text;
            Zoom = state.Zoom;
            IsSaved = state.IsSaved;
            WordWrap = state.WordWrap;
            ShowLineNumbers = state.ShowLineNumbers;
        }

        public ToolState SaveState()
        {
            return new ToolState()
            {
                Title = Title,
                SavedState = Json.Json.GetJsonText(new NotepadState()
                {
                    FilePath = FilePath,
                    Text = Text,
                    Zoom = Zoom,
                    IsSaved = IsSaved,
                    WordWrap = WordWrap,
                    ShowLineNumbers = ShowLineNumbers
                }),
            };
        }

        private static string BuildOpenFilter() =>
            $"{Texts.NotepadDocument}|*.txt;*{NotepadDocumentSerializer.PackageExtension}|" +
            $"{Texts.TextFile}|*.txt|" +
            $"{Texts.RichNotepadFile}|*{NotepadDocumentSerializer.PackageExtension}";

        private static string BuildSaveFilter(string preferredExtension) =>
            string.Equals(preferredExtension, NotepadDocumentSerializer.PackageExtension, StringComparison.OrdinalIgnoreCase)
                ? $"{Texts.RichNotepadFile}|*{NotepadDocumentSerializer.PackageExtension}"
                : $"{Texts.TextFile}|*.txt";

        private static string BuildDefaultFileName(string preferredExtension) =>
            $"{Texts.Untitled}{preferredExtension}";

        private static bool MatchesPreferredExtension(string filePath, string preferredExtension) =>
            string.Equals(Path.GetExtension(filePath), preferredExtension, StringComparison.OrdinalIgnoreCase);

        private static string EnsureExtension(string filePath, string preferredExtension) =>
            MatchesPreferredExtension(filePath, preferredExtension)
                ? filePath
                : Path.ChangeExtension(filePath, preferredExtension);
    }
}

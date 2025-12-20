using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
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
        public bool IsSaved { get; set => Set(ref field, value); } = true;
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

        public OpenFileDialogViewModel OpenFileDialog { get; } = new();
        public SaveFileDialogViewModel SaveFileDialog { get; } = new();

        public ICommand ChangeFontSizeCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand OpenNewTabCommand { get; }

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
                    var filePath = OpenFileDialog.Show($"{Texts.TextFile}|*.txt;");
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                        return;
                    try
                    {
                        var bytes = File.ReadAllBytes(FilePath);
                        var encoding = EncodingChecker.GetAvailableEncodings(bytes).FirstOrDefault() ?? Encoding.UTF8;
                        Text = encoding.GetString(bytes);
                        FilePath = filePath;
                        IsSaved = true;
                    }
                    catch (Exception ex)
                    {
                        var message = $"{Texts.FailedToOpenFile}\r\n{ex.Message}";
                        Log.Default.Write(message, ex);
                        MessageBox.Show($"{Texts.FailedToOpenFile}\r\n{ex.Message}", Texts.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            SaveCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    var filePath = x as string;
                    if (string.IsNullOrEmpty(filePath))
                    {
                        filePath = SaveFileDialog.Show($"{Texts.TextFile}|*.txt;", Texts.Untitled);
                        if (string.IsNullOrEmpty(filePath))
                            return;
                        FilePath = filePath;
                    }
                    try
                    {
                        File.WriteAllText(filePath, Text, Encoding.UTF8);
                        IsSaved = true;
                    }
                    catch (Exception ex)
                    {
                        var message = $"{Texts.FailedToSaveFile}\r\n{ex.Message}";
                        Log.Default.Write(message, ex);
                        MessageBox.Show($"{Texts.FailedToSaveFile}\r\n{ex.Message}", Texts.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            OpenNewTabCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var notepadState = new NotepadState()
                    {
                        Zoom = Zoom
                    };
                    var toolState = new ToolState() 
                    { 
                        SavedState = Json.Json.GetJsonText(notepadState) 
                    };
                    var args = new CreateNewToolViewRequestedEventArgs(toolState);
                    CreateNewToolViewRequested?.Invoke(this, args);
                });

        }

        public void LoadState(ToolState stateData)
        {
            if(stateData.SavedState is null)
                return;
            var state = Json.Json.LoadFromText<NotepadState>(stateData.SavedState);
            if(state is null)
                return;
            FilePath = state.FilePath;
            Text = state.Text;
            Zoom = state.Zoom;
            IsSaved = state.IsSaved;
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
                }),
            };
        }
    }
}

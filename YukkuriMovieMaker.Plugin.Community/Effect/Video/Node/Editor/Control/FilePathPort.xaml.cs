using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

public partial class FilePathPort : INotifyPropertyChanged
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(FilePathPort),
            new FrameworkPropertyMetadata(
                "",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty AllowExtensionProperty =
        DependencyProperty.Register(
            nameof(AllowExtension),
            typeof(List<(string Name, string[] Ext)>),
            typeof(FilePathPort),
            new PropertyMetadata(new List<(string, string[])>()));

    public FilePathPort()
    {
        InitializeComponent();
        OpenFileCommand = new RelayCommand(OpenFileDialog);
    }

    public ICommand OpenFileCommand { get; }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public List<(string Name, string[] Ext)> AllowExtension
    {
        get => (List<(string Name, string[] Ext)>)GetValue(AllowExtensionProperty);
        init => SetValue(AllowExtensionProperty, value);
    }

    public string PathFileText =>
        string.IsNullOrEmpty(Value)
            ? ""
            : Path.GetFileName(Value);

    public event PropertyChangedEventHandler? PropertyChanged;

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FilePathPort)d;
        control.OnPropertyChanged(nameof(Value));
        control.OnPropertyChanged(nameof(PathFileText));
    }

    private void OpenFileDialog()
    {
        if (AllowExtension.Count == 0)
            return;

        var dialog = new OpenFileDialog
        {
            DefaultExt = AllowExtension.First().Ext.FirstOrDefault() ?? "*.*",
            Filter = string.Join("|",
                AllowExtension.Select(nameExtPair =>
                    $"{nameExtPair.Name}|{string.Join(";", nameExtPair.Ext.Select(ext => "*" + ext))}"))
        };

        if (dialog.ShowDialog() == true)
            Value = dialog.FileName;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
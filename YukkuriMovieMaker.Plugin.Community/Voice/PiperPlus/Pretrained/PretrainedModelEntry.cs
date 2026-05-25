using System.IO;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;

internal sealed class PretrainedModelEntry : Bindable
{
    readonly PretrainedModelDefinition definition;

    bool isDownloading;
    bool isDownloaded;
    double progressValue;
    string progressMessage = string.Empty;

    public string DisplayName => definition.DisplayName;
    public string Languages => definition.Languages;
    public string Description => definition.Description;

    public bool IsDownloading
    {
        get => isDownloading;
        private set => Set(ref isDownloading, value);
    }

    public bool IsDownloaded
    {
        get => isDownloaded;
        private set => Set(ref isDownloaded, value);
    }

    public double ProgressValue
    {
        get => progressValue;
        private set => Set(ref progressValue, value);
    }

    public string ProgressMessage
    {
        get => progressMessage;
        private set => Set(ref progressMessage, value);
    }

    public ICommand DownloadCommand { get; }

    public PretrainedModelEntry(PretrainedModelDefinition definition)
    {
        this.definition = definition;
        DownloadCommand = new ActionCommand(_ => !IsDownloading, async _ => await DownloadAsync());
        RefreshDownloadedState();
    }

    public void RefreshDownloadedState()
    {
        var modelRoot = PiperPlusSettings.Default.ModelDirectory;
        var onnxPath = Path.Combine(modelRoot, definition.SubDirectory, definition.OnnxFileName);
        var jsonPath = onnxPath + ".json";
        IsDownloaded = File.Exists(onnxPath) && File.Exists(jsonPath);
    }

    async Task DownloadAsync()
    {
        IsDownloading = true;
        ProgressValue = 0;
        ProgressMessage = string.Empty;

        try
        {
            var progress = new Progress<(double Progress, string Message)>(report =>
            {
                ProgressValue = report.Progress;
                ProgressMessage = report.Message;
            });

            await PretrainedModelDownloader.DownloadAsync(definition, progress);
            RefreshDownloadedState();
        }
        catch (Exception ex)
        {
            ProgressMessage = ex.Message;
        }
        finally
        {
            IsDownloading = false;
        }
    }
}

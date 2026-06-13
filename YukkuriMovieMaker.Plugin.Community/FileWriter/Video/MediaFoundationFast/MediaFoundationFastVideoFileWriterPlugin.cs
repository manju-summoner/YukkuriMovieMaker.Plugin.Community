using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Localization;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.ViewModels;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Views;
using YukkuriMovieMaker.Plugin.FileWriter;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast;

public sealed class MediaFoundationFastVideoFileWriterPlugin : IVideoFileWriterPlugin
{
    public string Name => Texts.PluginName;

    public VideoFileWriterOutputPath OutputPathMode => VideoFileWriterOutputPath.File;

    public IVideoFileWriter CreateVideoFileWriter(string path, VideoInfo videoInfo)
    {
        return new MediaFoundationFastVideoFileWriter(path, videoInfo, MediaFoundationFastWriterSettings.Default);
    }

    public string GetFileExtention() => ".mp4";

    public UIElement GetVideoConfigView(string projectName, VideoInfo videoInfo, int length)
    {
        return new MediaFoundationFastConfigView
        {
            DataContext = new MediaFoundationFastConfigViewModel(),
        };
    }

    public bool NeedDownloadResources() => false;

    public Task DownloadResources(ProgressMessage progress, CancellationToken token) => Task.CompletedTask;
}

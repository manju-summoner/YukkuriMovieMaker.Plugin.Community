using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Localization;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast;

public class MediaFoundationFastWriterSettings : SettingsBase<MediaFoundationFastWriterSettings>
{
    public override SettingsCategory Category => SettingsCategory.VideoFileWriter;
    public override string Name => Texts.PluginName;
    public override bool HasSettingView => false;
    public override object? SettingView => null;

    public VideoBitRateControlMode VideoBitRateControlMode { get => videoBitRateControlMode; set => Set(ref videoBitRateControlMode, value); }
    VideoBitRateControlMode videoBitRateControlMode = VideoBitRateControlMode.Quality;

    public int VideoBitRate { get => videoBitRate; set => Set(ref videoBitRate, value); }
    int videoBitRate;

    public int VideoQuality { get => videoQuality; set => Set(ref videoQuality, value); }
    int videoQuality = 70;

    public int EncodeSpeed { get => encodeSpeed; set => Set(ref encodeSpeed, value); }
    int encodeSpeed = 50;

    public int NumberOfThreads { get => numberOfThreads; set => Set(ref numberOfThreads, value); }
    int numberOfThreads;

    public int GOPSize { get => gopSize; set => Set(ref gopSize, value); }
    int gopSize;

    public int BFrameCount { get => bFrameCount; set => Set(ref bFrameCount, value); }
    int bFrameCount = 2;

    public H264Profile H264Profile { get => h264Profile; set => Set(ref h264Profile, value); }
    H264Profile h264Profile = H264Profile.High;

    public H264Level H264Level { get => h264Level; set => Set(ref h264Level, value); }
    H264Level h264Level = H264Level.Auto;

    public bool IsHardwareAcceleration { get => isHardwareAcceleration; set => Set(ref isHardwareAcceleration, value); }
    bool isHardwareAcceleration = true;

    public AudioBitRate AudioBitRate { get => audioBitRate; set => Set(ref audioBitRate, value); }
    AudioBitRate audioBitRate = AudioBitRate.kb192;

    public AACProfile AACProfile { get => aacProfile; set => Set(ref aacProfile, value); }
    AACProfile aacProfile = AACProfile.AACL2;

    public int Width { get => width; set => Set(ref width, value); }
    int width;

    public int Height { get => height; set => Set(ref height, value); }
    int height;

    public int FPS { get => fps; set => Set(ref fps, value); }
    int fps;

    public int Hz { get => hz; set => Set(ref hz, value); }
    int hz;

    public int Length { get => length; set => Set(ref length, value); }
    int length;

    public override void Initialize()
    {
    }
}

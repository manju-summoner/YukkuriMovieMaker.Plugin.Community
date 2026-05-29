using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusVoiceSpeaker(PiperSpeakerEntry entry, IVoiceResource? resource = null) : IVoiceSpeaker
{
    const int ProcessTimeoutMs = 120_000;

    static readonly SemaphoreSlim Semaphore = new(1, 1);

    public string EngineName => "Piper Plus";
    public string SpeakerName => entry.DisplayName;
    public string API => "PiperPlus";
    public string ID => entry.UniqueId;
    public bool IsVoiceDataCachingRequired => true;
    public SupportedTextFormat Format => SupportedTextFormat.Text;
    public IVoiceLicense? License => null;
    public IVoiceResource? Resource => resource;

    public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
        => throw new NotImplementedException();

    public async Task<IVoicePronounce?> CreateVoiceAsync(
        string text,
        IVoicePronounce? pronounce,
        IVoiceParameter? parameter,
        string filePath)
    {
        var param = parameter as PiperPlusVoiceParameter ?? new PiperPlusVoiceParameter();

        if (!PiperBinaryResource.IsReady)
        {
            await PiperBinaryResource.EnsureAsync(PiperBinaryResource.Version);
        }

        var executablePath = PiperBinaryResource.ExecutablePath;

        if (!File.Exists(entry.ModelPath))
            throw new FileNotFoundException($"Model not found: {entry.ModelPath}");

        await Semaphore.WaitAsync();
        try
        {
            await Task.Run(() => RunPiper(executablePath, text, param, filePath));
        }
        finally
        {
            Semaphore.Release();
        }

        return null;
    }

    void RunPiper(
        string executablePath,
        string text,
        PiperPlusVoiceParameter param,
        string filePath)
    {
        var workingDirectory = Path.GetDirectoryName(executablePath)
            ?? throw new DirectoryNotFoundException($"Cannot resolve directory of: {executablePath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(entry.ModelPath);
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(entry.ConfigPath);
        startInfo.ArgumentList.Add("--output-file");
        startInfo.ArgumentList.Add(filePath);
        startInfo.ArgumentList.Add("--text");
        startInfo.ArgumentList.Add(PhonemeNotationConverter.Convert(text));
        startInfo.ArgumentList.Add("--length-scale");
        startInfo.ArgumentList.Add(param.LengthScale.ToString("F3", CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--noise-scale");
        startInfo.ArgumentList.Add(param.NoiseScale.ToString("F3", CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--noise-w");
        startInfo.ArgumentList.Add(param.NoiseScaleW.ToString("F3", CultureInfo.InvariantCulture));

        if (entry.IsMultiSpeaker)
        {
            startInfo.ArgumentList.Add("--speaker");
            startInfo.ArgumentList.Add(entry.SpeakerId.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(entry.LanguageArgument))
        {
            startInfo.ArgumentList.Add("--language");
            startInfo.ArgumentList.Add(entry.LanguageArgument);
        }

        startInfo.Environment["APPDATA"] = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        startInfo.Environment["USERPROFILE"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        startInfo.Environment["TEMP"] = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        startInfo.Environment["TMP"] = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        var stderrBuilder = new StringBuilder();

        using var process = new Process { StartInfo = startInfo };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is { } data)
                stderrBuilder.AppendLine(data);
        };

        process.Start();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(ProcessTimeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException(
                $"Piper Plus process did not complete within {ProcessTimeoutMs / 1000} seconds.");
        }

        process.WaitForExit();

        string StderrDetail()
        {
            var s = stderrBuilder.ToString().Trim();
            return s.Length > 0 ? $"\n{s}" : string.Empty;
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Piper Plus exited with code {process.ExitCode}.{StderrDetail()}");

        if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            throw new InvalidOperationException(
                $"Piper Plus produced no output at: {filePath}{StderrDetail()}");
    }

    public IVoiceParameter CreateVoiceParameter() => new PiperPlusVoiceParameter();

    public bool IsMatch(string api, string id) => api == API && id == ID;

    public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
        => currentParameter is PiperPlusVoiceParameter ? currentParameter : CreateVoiceParameter();
}

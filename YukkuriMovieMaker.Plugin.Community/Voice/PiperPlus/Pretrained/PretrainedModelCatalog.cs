using System.IO;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;

internal sealed record PretrainedModelDefinition(
    string OnnxFileName,
    string OnnxUrl,
    string ConfigUrl
)
{
    public string ModelName => Path.GetFileNameWithoutExtension(OnnxFileName);
    public string ModelPath => Path.Combine(PiperPlusPaths.ModelDirectory, OnnxFileName);
    public string ConfigPath => ModelPath + ".json";
}

internal static class PretrainedModelCatalog
{
    const string TsukuyomiBase = "https://huggingface.co/ayousanz/piper-plus-tsukuyomi-chan/resolve/main";
    const string Css10Base = "https://huggingface.co/ayousanz/piper-plus-css10-ja-6lang/resolve/main";

    public static IReadOnlyList<PretrainedModelDefinition> All { get; } =
    [
        new(
            OnnxFileName: "tsukuyomi-chan-6lang-fp16.onnx",
            OnnxUrl: $"{TsukuyomiBase}/tsukuyomi-chan-6lang-fp16.onnx",
            ConfigUrl: $"{TsukuyomiBase}/config.json"
        ),
        new(
            OnnxFileName: "css10-ja-6lang-fp16.onnx",
            OnnxUrl: $"{Css10Base}/css10-ja-6lang-fp16.onnx",
            ConfigUrl: $"{Css10Base}/config.json"
        ),
    ];
}

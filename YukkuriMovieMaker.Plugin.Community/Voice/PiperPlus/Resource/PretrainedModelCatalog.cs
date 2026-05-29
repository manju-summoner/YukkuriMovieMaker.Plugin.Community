namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class PretrainedModelCatalog
{
    const string TsukuyomiBase = "https://huggingface.co/ayousanz/piper-plus-tsukuyomi-chan/resolve/main";
    const string Css10Base = "https://huggingface.co/ayousanz/piper-plus-css10-ja-6lang/resolve/main";

    public static IReadOnlyList<PretrainedModelCatalogItem> All { get; } =
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

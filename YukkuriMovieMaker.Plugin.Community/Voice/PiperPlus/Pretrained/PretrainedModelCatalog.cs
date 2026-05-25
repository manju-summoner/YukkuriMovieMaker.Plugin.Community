namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;

internal sealed record PretrainedModelDefinition(
    string Id,
    string DisplayName,
    string Languages,
    string Description,
    string SubDirectory,
    string OnnxFileName,
    string OnnxUrl,
    string ConfigUrl
);

internal static class PretrainedModelCatalog
{
    const string TsukuyomiBase = "https://huggingface.co/ayousanz/piper-plus-tsukuyomi-chan/resolve/main";
    const string Css10Base = "https://huggingface.co/ayousanz/piper-plus-css10-ja-6lang/resolve/main";

    public static IReadOnlyList<PretrainedModelDefinition> All { get; } =
    [
        new(
            Id: "tsukuyomi-mb-istft",
            DisplayName: "Tsukuyomi-chan MB-iSTFT",
            Languages: "JA / EN / ZH / ES / FR / PT",
            Description: "500 epochs (2026-05-02) · MB-iSTFT-VITS2 · 2.21× faster CPU inference (61.9 ms p50) · FP16 · ONNX 38 MB",
            SubDirectory: "tsukuyomi-mb-istft",
            OnnxFileName: "tsukuyomi-chan-6lang-fp16.onnx",
            OnnxUrl: $"{TsukuyomiBase}/tsukuyomi-chan-6lang-fp16.onnx",
            ConfigUrl: $"{TsukuyomiBase}/config.json"
        ),
        new(
            Id: "css10-ja-6lang",
            DisplayName: "CSS10 Japanese 6lang",
            Languages: "JA / EN / ZH / ES / FR / PT",
            Description: "50 epochs · 6,841 utterances · MB-iSTFT-VITS2 · CSS10 Japanese voice · FP16 · ONNX 38 MB",
            SubDirectory: "css10-ja-6lang",
            OnnxFileName: "css10-ja-6lang-fp16.onnx",
            OnnxUrl: $"{Css10Base}/css10-ja-6lang-fp16.onnx",
            ConfigUrl: $"{Css10Base}/config.json"
        ),
    ];
}

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class PretrainedModelCatalog
{
    const string TsukuyomiBase = "https://huggingface.co/ayousanz/piper-plus-tsukuyomi-chan/resolve/main";
    const string Css10Base = "https://huggingface.co/ayousanz/piper-plus-css10-ja-6lang/resolve/main";

    // 上流のモデルガイドが revision を固定した URL で案内しているため main ではなく revision で取得する。
    // 配布名 tsukuyomi.onnx はほかのモデルとぶつかりやすいので、保存名は別に付ける
    const string TsukuyomiZsV1Base = "https://huggingface.co/ayousanz/piper-plus-tsukuyomi-chan/resolve/93c1fb6d67c29accc9ddf52326bb4582b1b52a3d/releases/zs-v1";

    public static IReadOnlyList<PretrainedModelCatalogItem> All { get; } =
    [
        new(
            OnnxFileName: "tsukuyomi-chan-6lang-fp16.onnx",
            OnnxUrl: $"{TsukuyomiBase}/tsukuyomi-chan-6lang-fp16.onnx",
            ConfigUrl: $"{TsukuyomiBase}/config.json",
            TermsUrl: "https://huggingface.co/ayousanz/piper-plus-tsukuyomi-chan"
        ),
        new(
            OnnxFileName: "tsukuyomi-chan-zs-v1.onnx",
            OnnxUrl: $"{TsukuyomiZsV1Base}/tsukuyomi.onnx",
            ConfigUrl: $"{TsukuyomiZsV1Base}/tsukuyomi.onnx.json",
            TermsUrl: "https://huggingface.co/ayousanz/piper-plus-tsukuyomi-chan/blob/93c1fb6d67c29accc9ddf52326bb4582b1b52a3d/releases/zs-v1/NOTICE.md"
        ),
        new(
            OnnxFileName: "css10-ja-6lang-fp16.onnx",
            OnnxUrl: $"{Css10Base}/css10-ja-6lang-fp16.onnx",
            ConfigUrl: $"{Css10Base}/config.json",
            TermsUrl: "https://huggingface.co/ayousanz/piper-plus-css10-ja-6lang"
        ),
    ];
}

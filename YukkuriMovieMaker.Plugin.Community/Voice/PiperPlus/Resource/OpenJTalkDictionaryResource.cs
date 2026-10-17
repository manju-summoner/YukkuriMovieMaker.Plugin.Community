using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

/// <summary>
/// CLI が日本語の読み上げに使う OpenJTalk 辞書。
/// </summary>
/// <remarks>
/// 辞書が見つからないと CLI は合成の途中で自分でダウンロードを始めるが、進捗が出ず、
/// 合成の打ち切り時間（<see cref="PiperPlusVoiceSpeaker"/>）に間に合わないと次回も最初からやり直しになる。
/// そのため YMM4 がエンジンと一緒に導入し、合成時に OPENJTALK_DICTIONARY_PATH で場所を渡す。
/// URL・SHA256・必須ファイルは上流 v2.0.2 の PiperPlus.Core/Config/DictionaryManager.cs に合わせている。
/// </remarks>
internal static class OpenJTalkDictionaryResource
{
    public const string DirectoryName = "open_jtalk_dic_utf_8-1.11";

    public static readonly OpenJTalkDictionarySource Source = new(
        "https://github.com/r9y9/open_jtalk/releases/download/v1.11.1/open_jtalk_dic_utf_8-1.11.tar.gz",
        "fe6ba0e43542cef98339abdffd903e062008ea170b04e7e2a35da805902f382a");

    static readonly string[] RequiredFiles = ["sys.dic", "matrix.bin", "char.bin", "unk.dic"];

    public static bool IsValid(string directory) =>
        Directory.Exists(directory) && RequiredFiles.All(x => File.Exists(Path.Combine(directory, x)));

    public static async Task InstallAsync(
        HttpClient client,
        OpenJTalkDictionarySource source,
        string targetDir,
        double startFraction,
        double endFraction,
        ProgressMessage? progress,
        CancellationToken cancellationToken)
    {
        //展開途中のフォルダが辞書として使われないよう、同じボリュームの作業用フォルダで組み立ててから名前を変える
        var stagingDir = targetDir + ".tmp";
        if (Directory.Exists(stagingDir))
            Directory.Delete(stagingDir, recursive: true);
        Directory.CreateDirectory(stagingDir);

        try
        {
            var archivePath = Path.Combine(stagingDir, DirectoryName + ".tar.gz");
            var extractedFraction = startFraction + (endFraction - startFraction) * 0.9;

            await ProgressiveIo.DownloadFileAsync(client, source.Url, archivePath,
                startFraction, extractedFraction,
                Texts.DownloadingDictionary, progress, cancellationToken);

            progress?.Report(extractedFraction, Texts.ExtractingDictionary);
            await VerifySha256Async(archivePath, source.Sha256, cancellationToken);

            var extractDir = Path.Combine(stagingDir, "extract");
            await ExtractTarGzAsync(archivePath, extractDir, cancellationToken);

            var stagedDictionary = Path.Combine(extractDir, DirectoryName);
            if (!IsValid(stagedDictionary))
                throw new InvalidDataException(
                    $"OpenJTalk dictionary is incomplete after extraction. Expected {string.Join(", ", RequiredFiles)} in '{stagedDictionary}'.");

            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);
            Directory.Move(stagedDictionary, targetDir);

            progress?.Report(endFraction, Texts.ExtractingDictionary);
        }
        finally
        {
            try { Directory.Delete(stagingDir, recursive: true); } catch { }
        }
    }

    static async Task VerifySha256Async(string path, string expected, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"OpenJTalk dictionary SHA256 mismatch: expected {expected}, got {actual}.");
    }

    static async Task ExtractTarGzAsync(string archivePath, string destinationDir, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDir);
        var resolvedDestination = Path.GetFullPath(destinationDir) + Path.DirectorySeparatorChar;

        await using var fileStream = File.OpenRead(archivePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        await using var reader = new TarReader(gzipStream);

        while (await reader.GetNextEntryAsync(cancellationToken: cancellationToken) is { } entry)
        {
            var destPath = Path.GetFullPath(Path.Combine(destinationDir, entry.Name));
            if (!destPath.StartsWith(resolvedDestination, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Entry '{entry.Name}' would extract outside destination directory.");

            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(destPath);
                    break;
                case TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile:
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    await entry.ExtractToFileAsync(destPath, overwrite: true, cancellationToken);
                    break;
                default:
                    //シンボリックリンク・ハードリンクは展開先の外を指しうるので置かない。辞書は通常のファイルだけで構成される
                    break;
            }
        }
    }
}

internal sealed record OpenJTalkDictionarySource(string Url, string Sha256);

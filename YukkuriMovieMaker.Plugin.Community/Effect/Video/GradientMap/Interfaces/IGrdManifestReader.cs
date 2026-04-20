using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

public interface IGrdManifestReader
{
    GrdManifest Read(string filePath);
}

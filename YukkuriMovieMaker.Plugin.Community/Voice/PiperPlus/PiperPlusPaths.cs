using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal static class PiperPlusPaths
{
    public static string BinaryDirectory => Path.Combine(AppDirectories.UserResourceDirectory, "piper");

    public static string ModelDirectory => Path.Combine(BinaryDirectory, "models");
}

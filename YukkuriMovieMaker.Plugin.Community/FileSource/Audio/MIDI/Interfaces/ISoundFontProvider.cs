namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;

internal interface ISoundFontProvider
{
    IReadOnlyList<string> GetActiveSoundFontPaths();
    bool HasAnySoundFont();
}

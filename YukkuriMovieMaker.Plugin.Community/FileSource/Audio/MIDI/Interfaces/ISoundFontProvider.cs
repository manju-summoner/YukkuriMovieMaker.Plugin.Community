namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Interfaces;

internal interface ISoundFontProvider
{
    IReadOnlyList<(string Path, float Volume)> GetActiveSoundFontPaths();
    bool HasAnySoundFont();
}

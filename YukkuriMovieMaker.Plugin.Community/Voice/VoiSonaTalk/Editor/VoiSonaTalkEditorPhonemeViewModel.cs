using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorPhonemeViewModel(VoiSonaTalkVoicePronounce pronounce, int index) : Bindable
    {
        public string Phoneme =>
            (pronounce.Phonemes is { } phonemes && index < phonemes.Length)
                ? phonemes[index]
                : string.Empty;

        public double Duration
        {
            get => (pronounce.PhonemeDurations is { } durations && index < durations.Length)
                ? durations[index]
                : 0;
            set
            {
                if (pronounce.PhonemeDurations is null || index >= pronounce.PhonemeDurations.Length)
                    return;
                var corrected = value < 0 ? -1 : Math.Min(value, 10);
                if (pronounce.PhonemeDurations[index] == corrected)
                    return;

                var newDurations = (double[])pronounce.PhonemeDurations.Clone();
                newDurations[index] = corrected;
                pronounce.PhonemeDurations = newDurations;
                OnPropertyChanged(nameof(Duration));
            }
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Phoneme));
            OnPropertyChanged(nameof(Duration));
        }
    }
}

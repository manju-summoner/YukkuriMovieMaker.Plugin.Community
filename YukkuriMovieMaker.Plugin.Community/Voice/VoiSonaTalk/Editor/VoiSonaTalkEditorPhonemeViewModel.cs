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
            get
            {
                // ピン値は要求値であり、エンジンが生成可能な値へ調整することがあるため実測値と一致するとは限らない
                if (pronounce.RequestedPhonemeDurations is { } requested && index < requested.Length && requested[index] >= 0)
                    return requested[index];
                return (pronounce.PhonemeDurations is { } durations && index < durations.Length)
                    ? durations[index]
                    : 0;
            }
            set
            {
                if (!double.IsFinite(value))
                {
                    // 入力欄の表示を現在の値へ巻き戻す（.NETのdouble.Parseは"NaN"等を受理する）
                    OnPropertyChanged(nameof(Duration));
                    return;
                }
                if (pronounce.Phonemes is not { } phonemes || index >= phonemes.Length)
                    return;
                var corrected = value < 0 ? -1 : Math.Min(value, 10);

                var requested = pronounce.RequestedPhonemeDurations ?? [];
                var current = index < requested.Length ? requested[index] : -1;
                if (current == corrected)
                {
                    // 値が変わらなくても表示は編集後の文字列のままなので、現在の値へ巻き戻す
                    OnPropertyChanged(nameof(Duration));
                    return;
                }

                // 編集した音素だけを固定し、他は-1（=エンジンの自動算出）のままにする
                var newRequested = new double[phonemes.Length];
                Array.Fill(newRequested, -1);
                Array.Copy(requested, newRequested, Math.Min(requested.Length, newRequested.Length));
                newRequested[index] = corrected;
                pronounce.RequestedPhonemeDurations = newRequested;
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

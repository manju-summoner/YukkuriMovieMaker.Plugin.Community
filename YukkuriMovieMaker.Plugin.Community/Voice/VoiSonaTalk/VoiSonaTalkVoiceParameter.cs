using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkVoiceParameter : VoiceParameterBase
    {
        string language = string.Empty, version = string.Empty;
        double 
            alp = 0,
            huskiness = 0,
            intonation = 1,
            pitch = 0,
            speed = 1;
        ImmutableList<VoiSonaTalkStyleWeight> styleWeights = [];

        [Display(Name = nameof(Texts.Speed), Description = nameof(Texts.Speed), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", 0.2d, 5)]
        [Range(0.2d, 5d)]
        [DefaultValue(1d)]
        public double Speed { get => speed; set => Set(ref speed, value); }

        [Display(Name = nameof(Texts.Pitch), Description = nameof(Texts.Pitch), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", -600d, 600d)]
        [Range(-600d, 600d)]
        [DefaultValue(0d)]
        public double Pitch { get => pitch; set => Set(ref pitch, value); }

        [Display(Name = nameof(Texts.Intonation), Description = nameof(Texts.Intonation), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", 0, 2)]
        [Range(0d, 2d)]
        [DefaultValue(1d)]
        public double Intonation { get => intonation; set => Set(ref intonation, value); }

        [Display(Name = nameof(Texts.Alp), Description = nameof(Texts.Alp), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", -1, 1)]
        [Range(-1d, 1d)]
        [DefaultValue(0d)]
        public double Alp { get => alp; set => Set(ref alp, value); }

        [Display(Name = nameof(Texts.Huskiness), Description = nameof(Texts.Huskiness), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", -10d, 10d)]
        [Range(-10d, 10d)]
        [DefaultValue(0d)]
        public double Huskiness { get => huskiness; set => Set(ref huskiness, value); }


        [Display(GroupName = null, Name = null, AutoGenerateField = true)]
        public ImmutableList<VoiSonaTalkStyleWeight> StyleWeights
        { 
            get => styleWeights;
            set
            {
                var oldStyleWeights = styleWeights;
                if(Set(ref styleWeights, value))
                {
                    foreach(var sw in oldStyleWeights)
                        sw.PropertyChanged -= StyleWeight_PropertyChanged;
                    foreach(var sw in styleWeights)
                        sw.PropertyChanged += StyleWeight_PropertyChanged;
                }
            }
        }

        [Display(Name = nameof(Texts.Language), Description = nameof(Texts.Language), ResourceType = typeof(Texts))]
        [VoiSonaTalkLanguageComboBox]
        public string Language { get => language; set => Set(ref language, value); }

        [Display(Name = nameof(Texts.Version), Description = nameof(Texts.Version), ResourceType = typeof(Texts))]
        [VoiSonaTalkVersionComboBox]
        public string Version { get => version; set => Set(ref version, value); }

        private void StyleWeight_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 子プロパティの変更を伝搬させる（PropertyNameは適当）
            // これが無いと音声ファイルが再生成されない
            OnPropertyChanged($"{nameof(StyleWeights)}[{StyleWeights.IndexOf((VoiSonaTalkStyleWeight)sender!)}].{e.PropertyName}");
        }
    }
}

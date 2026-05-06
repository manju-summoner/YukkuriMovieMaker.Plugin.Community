using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    internal class BandsEditorSettings : SettingsBase<BandsEditorSettings>
    {
        public override SettingsCategory Category => SettingsCategory.AudioEffect;

        public override string Name => Texts.Equalizer2;

        public override bool HasSettingView => false;

        public override object? SettingView => null;

        double editorHeight = 200d;

        public double EditorHeight { get => editorHeight; set => Set(ref editorHeight, value); }

        public override void Initialize()
        {

        }
    }
}

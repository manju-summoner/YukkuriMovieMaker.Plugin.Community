using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater.AudioSourceReader
{
    public class SceneAudioSourceReaderParameter : AudioSourceReaderParameterBase
    {
        [Display(Name = nameof(Texts.Scene), Description = nameof(Texts.Scene), ResourceType = typeof(Texts))]
        [SceneComboBox]
        public Guid SceneId { get => sceneId; set => Set(ref sceneId, value); }
        Guid sceneId = Guid.Empty;

        public SceneAudioSourceReaderParameter() { }

        public SceneAudioSourceReaderParameter(SharedDataStore? store = null) : base(store) { }

        public override AudioSourceReaderBase CreateReader()
        {
            return new SceneAudioSourceReader(this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];

        protected override void LoadSharedData(SharedDataStore store)
        {
            var sharedData = store.Load<SharedData>();
            if (sharedData is null)
                return;

            sharedData.CopyTo(this);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new SharedData(this));
        }

        private class SharedData
        {
            public Guid SceneId { get => sceneId; set => sceneId = value; }
            Guid sceneId = Guid.Empty;

            public SharedData(SceneAudioSourceReaderParameter param)
            {
                SceneId = param.SceneId;
            }

            public void CopyTo(SceneAudioSourceReaderParameter param)
            {
                param.SceneId = SceneId;
            }
        }
    }
}

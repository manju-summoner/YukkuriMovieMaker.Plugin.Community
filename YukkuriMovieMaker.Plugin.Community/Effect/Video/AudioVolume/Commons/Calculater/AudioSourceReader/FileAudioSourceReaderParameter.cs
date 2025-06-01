using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.Calculater.AudioSourceReader
{
    public class FileAudioSourceReaderParameter : AudioSourceReaderParameterBase, IFileItem
    {
        [Display(Name = nameof(Texts.File), Description = nameof(Texts.File), ResourceType = typeof(Texts))]
        [FileSelector(Settings.FileGroupType.AudioItem)]
        public string File { get => file; set => Set(ref file, value); }
        string file = string.Empty;

        public FileAudioSourceReaderParameter() : base() { }

        public FileAudioSourceReaderParameter(SharedDataStore? sharedDataStore = null) : base(sharedDataStore) { }

        public override AudioSourceReaderBase CreateReader()
        {
            return new FileAudioSourceReader(this);
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

        public IEnumerable<TimelineResource> GetResources()
        {
            if (TimelineResource.TryParseFromPath(File, TimelineResourceType.Audio, out var resource))
                yield return resource;
        }

        public IEnumerable<string> GetFiles()
        {
            if (!string.IsNullOrEmpty(File))
                yield return File;
        }

        public void ReplaceFile(string from, string to)
        {
            if (from == File)
                File = to;
        }

        private class SharedData
        {
            public string File { get => file; set => file = value; }
            string file = string.Empty;

            public SharedData(FileAudioSourceReaderParameter param)
            {
                File = param.File;
            }

            public void CopyTo(FileAudioSourceReaderParameter param)
            {
                param.File = file;
            }
        }
    }
}

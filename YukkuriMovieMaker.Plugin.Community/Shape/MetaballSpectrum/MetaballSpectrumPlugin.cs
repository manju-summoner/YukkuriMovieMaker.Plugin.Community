using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.MetaballSpectrum
{
    internal class MetaballSpectrumPlugin : IAudioSpectrumPlugin
    {
        public string Name => Texts.MetaballSpectrum;

        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IAudioSpectrumParameter CreateAudioSpectrumParameter(SharedDataStore? sharedData)
        {
            return new MetaballSpectrumParameter(sharedData);
        }
    }
}

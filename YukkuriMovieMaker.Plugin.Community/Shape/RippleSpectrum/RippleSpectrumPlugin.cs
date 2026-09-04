using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.RippleSpectrum
{
    internal class RippleSpectrumPlugin : IAudioSpectrumPlugin
    {
        public string Name => Texts.RippleSpectrum;

        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IAudioSpectrumParameter CreateAudioSpectrumParameter(SharedDataStore? sharedData)
        {
            return new RippleSpectrumParameter(sharedData);
        }
    }
}

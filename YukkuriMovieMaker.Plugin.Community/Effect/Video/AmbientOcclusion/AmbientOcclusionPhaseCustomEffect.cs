using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AmbientOcclusion
{
    internal sealed class AmbientOcclusionPhaseCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Sensitivity { set => SetValue((int)EffectImpl.Properties.Sensitivity, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            const int ReadRadius = 4;

            private ConstantBuffer _cb = new() { Sensitivity = 1f };

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Sensitivity)]
            public float Sensitivity
            {
                get => _cb.Sensitivity;
                set { _cb.Sensitivity = Math.Clamp(value, 0f, 4f); UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("AmbientOcclusionPhase"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var pad = ReadRadius + 1;
                inputRects[0] = new RawRect(
                    outputRect.Left - pad,
                    outputRect.Top - pad,
                    outputRect.Right + pad,
                    outputRect.Bottom + pad);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float Sensitivity;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }

            public enum Properties : int
            {
                Sensitivity = 0,
            }
        }
    }
}

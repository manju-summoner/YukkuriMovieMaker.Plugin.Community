using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ChannelRouter
{
    internal sealed class ChannelRouterCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public int SourceR
        {
            get => GetIntValue((int)EffectImpl.Properties.SourceR);
            set => SetValue((int)EffectImpl.Properties.SourceR, value);
        }

        public int SourceG
        {
            get => GetIntValue((int)EffectImpl.Properties.SourceG);
            set => SetValue((int)EffectImpl.Properties.SourceG, value);
        }

        public int SourceB
        {
            get => GetIntValue((int)EffectImpl.Properties.SourceB);
            set => SetValue((int)EffectImpl.Properties.SourceB, value);
        }

        public int SourceA
        {
            get => GetIntValue((int)EffectImpl.Properties.SourceA);
            set => SetValue((int)EffectImpl.Properties.SourceA, value);
        }

        public void SetCurrentInput(ID2D1Image? image) => SetInput(0, image, true);
        public void SetBranchInput(ID2D1Image? image) => SetInput(1, image, true);

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.SourceR)]
            public int SourceR
            {
                get => _cb.SourceR;
                set { _cb.SourceR = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.SourceG)]
            public int SourceG
            {
                get => _cb.SourceG;
                set { _cb.SourceG = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.SourceB)]
            public int SourceB
            {
                get => _cb.SourceB;
                set { _cb.SourceB = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.SourceA)]
            public int SourceA
            {
                get => _cb.SourceA;
                set { _cb.SourceA = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("ChannelRouter")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(
                RawRect[] inputRects,
                RawRect[] inputOpaqueSubRects,
                out RawRect outputRect,
                out RawRect outputOpaqueSubRect)
            {
                outputRect = inputRects.Length > 0 ? inputRects[0] : default;
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(
                RawRect outputRect,
                RawRect[] inputRects)
            {
                if (inputRects.Length > 0) inputRects[0] = outputRect;
                if (inputRects.Length > 1) inputRects[1] = new RawRect(-65536, -65536, 65536, 65536);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public int SourceR;
                public int SourceG;
                public int SourceB;
                public int SourceA;
            }

            public enum Properties
            {
                SourceR = 0,
                SourceG = 1,
                SourceB = 2,
                SourceA = 3,
            }
        }
    }
}

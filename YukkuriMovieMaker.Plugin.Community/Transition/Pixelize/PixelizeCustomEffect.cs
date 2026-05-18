using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YMM4SamplePlugin.Transition.Pixelize
{
    internal sealed class PixelizeCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float Progress
        {
            set => SetValue((int)EffectImpl.Properties.Progress, value);
            get => GetFloatValue((int)EffectImpl.Properties.Progress);
        }

        public float MaxBlockPx
        {
            set => SetValue((int)EffectImpl.Properties.MaxBlockPx, value);
            get => GetFloatValue((int)EffectImpl.Properties.MaxBlockPx);
        }

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Progress)]
            public float Progress
            {
                get => _cb.Progress;
                set { _cb.Progress = Math.Clamp(value, 0f, 1f); UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.MaxBlockPx)]
            public float MaxBlockPx
            {
                get => _cb.MaxBlockPx;
                set { _cb.MaxBlockPx = Math.Max(value, 1f); UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("PixelizeTransition")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var rect0 = ClampInputRect(inputRects[0]);
                var rect1 = ClampInputRect(inputRects[1]);

                inputRect = new RawRect(
                    Math.Min(rect0.Left, rect1.Left),
                    Math.Min(rect0.Top, rect1.Top),
                    Math.Max(rect0.Right, rect1.Right),
                    Math.Max(rect0.Bottom, rect1.Bottom));

                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    outputOpaqueSubRect = default;
                    return;
                }

                int w = inputRect.Right - inputRect.Left;
                int h = inputRect.Bottom - inputRect.Top;
                _cb.InputLeft = inputRect.Left;
                _cb.InputTop = inputRect.Top;
                _cb.InputWidth = w;
                _cb.InputHeight = h;
                UpdateConstants();

                outputRect = inputRect;
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                for (int i = 0; i < inputRects.Length; i++)
                    inputRects[i] = inputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Progress;
                public float MaxBlockPx;
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
                public float Pad0;
                public float Pad1;
            }

            public enum Properties
            {
                Progress = 0,
                MaxBlockPx = 1,
            }
        }
    }
}

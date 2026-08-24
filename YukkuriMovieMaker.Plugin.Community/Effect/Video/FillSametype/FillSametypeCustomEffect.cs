using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSametype
{
    internal sealed class FillSametypeCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public Vector4 TargetColor
        {
            set => SetValue((int)EffectImpl.Properties.TargetColor, value);
            get => GetVector4Value((int)EffectImpl.Properties.TargetColor);
        }

        public float Tolerance
        {
            set => SetValue((int)EffectImpl.Properties.Tolerance, value);
            get => GetFloatValue((int)EffectImpl.Properties.Tolerance);
        }

        public float Invert
        {
            set => SetValue((int)EffectImpl.Properties.Invert, value);
            get => GetFloatValue((int)EffectImpl.Properties.Invert);
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.TargetColor)]
            public Vector4 TargetColor
            {
                get => constants.TargetColor;
                set { constants.TargetColor = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Tolerance)]
            public float Tolerance
            {
                get => constants.Tolerance;
                set { constants.Tolerance = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Invert)]
            public float Invert
            {
                get => constants.Invert;
                set { constants.Invert = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("FillSamegroundShader"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 TargetColor;
                public float Tolerance;
                public float Invert;
                float _pad1;
                float _pad2;
            }

            public enum Properties : int
            {
                TargetColor = 0,
                Tolerance = 1,
                Invert = 2,
            }
        }
    }
}

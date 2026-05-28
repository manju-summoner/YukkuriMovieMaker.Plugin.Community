using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSameground
{
    internal class FillSamegroundCustomEffect : D2D1CustomShaderEffectBase
    {
        public System.Numerics.Vector4 TargetColor
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

        public FillSamegroundCustomEffect(IGraphicsDevicesAndContext devices)
            : base(Create<EffectImpl>(devices))
        {
        }

        [CustomEffect(1)]
        class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.TargetColor)]
            public System.Numerics.Vector4 TargetColor
            {
                get => new(constants.R, constants.G, constants.B, constants.A);
                set
                {
                    constants.R = value.X;
                    constants.G = value.Y;
                    constants.B = value.Z;
                    constants.A = value.W;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Tolerance)]
            public float Tolerance
            {
                get => constants.Tolerance;
                set
                {
                    constants.Tolerance = value;
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Invert)]
            public float Invert
            {
                get => constants.Invert;
                set
                {
                    constants.Invert = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(
                new Uri("pack://application:,,,/YukkuriMovieMaker.Plugin.Community;component/Resources/Shader/FillSamegroundShader.cso"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float R;
                public float G;
                public float B;
                public float A;
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

using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.Bevel
{
    internal sealed class BevelSdfSeedCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public Vector4 SourceRect { get => GetVector4Value((int)EffectImpl.Properties.SourceRect); set => SetValue((int)EffectImpl.Properties.SourceRect, value); }

        [CustomEffect(1)]
        sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.SourceRect)]
            public Vector4 SourceRect
            {
                get => constants.SourceRect;
                set { constants.SourceRect = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("BevelSdfSeed")) { }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four);
            }

            protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(constants);

            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
                => InflateAndClamp(invalidInputRect, 1, constants.SourceRect);

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                inputRects[0] = InflateAndClamp(outputRect, 1, constants.SourceRect);
            }

            static RawRect InflateAndClamp(RawRect rect, int amount, Vector4 bounds) => new(
                Math.Max((int)Math.Floor(bounds.X), rect.Left - amount),
                Math.Max((int)Math.Floor(bounds.Y), rect.Top - amount),
                Math.Min((int)Math.Ceiling(bounds.Z), rect.Right + amount),
                Math.Min((int)Math.Ceiling(bounds.W), rect.Bottom + amount));

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 SourceRect;
            }

            public enum Properties
            {
                SourceRect,
            }
        }
    }

    internal sealed class BevelSdfJumpCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public Vector4 SourceRect { get => GetVector4Value((int)EffectImpl.Properties.SourceRect); set => SetValue((int)EffectImpl.Properties.SourceRect, value); }
        public int StepSize { get => GetIntValue((int)EffectImpl.Properties.StepSize); set => SetValue((int)EffectImpl.Properties.StepSize, value); }

        [CustomEffect(1)]
        sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.SourceRect)]
            public Vector4 SourceRect
            {
                get => constants.SourceRect;
                set { constants.SourceRect = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.StepSize)]
            public int StepSize
            {
                get => constants.StepSize;
                set { constants.StepSize = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("BevelSdfJump")) { }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                drawInfo.SetInputDescription(0, new InputDescription { Filter = Filter.MinMagMipPoint, LevelOfDetailCount = 1 });
                drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four);
            }

            protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(constants);

            public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect)
            {
                var amount = Math.Max(1, constants.StepSize);
                return new RawRect(
                    Math.Max((int)Math.Floor(constants.SourceRect.X), invalidInputRect.Left - amount),
                    Math.Max((int)Math.Floor(constants.SourceRect.Y), invalidInputRect.Top - amount),
                    Math.Min((int)Math.Ceiling(constants.SourceRect.Z), invalidInputRect.Right + amount),
                    Math.Min((int)Math.Ceiling(constants.SourceRect.W), invalidInputRect.Bottom + amount));
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var amount = Math.Max(1, constants.StepSize);
                inputRects[0] = new RawRect(
                    Math.Max((int)Math.Floor(constants.SourceRect.X), outputRect.Left - amount),
                    Math.Max((int)Math.Floor(constants.SourceRect.Y), outputRect.Top - amount),
                    Math.Min((int)Math.Ceiling(constants.SourceRect.Z), outputRect.Right + amount),
                    Math.Min((int)Math.Ceiling(constants.SourceRect.W), outputRect.Bottom + amount));
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 SourceRect;
                public int StepSize;
            }

            public enum Properties
            {
                SourceRect,
                StepSize,
            }
        }
    }

    internal sealed class BevelSdfResolveCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public Vector4 SourceRect { get => GetVector4Value((int)EffectImpl.Properties.SourceRect); set => SetValue((int)EffectImpl.Properties.SourceRect, value); }
        public float Thickness { get => GetFloatValue((int)EffectImpl.Properties.Thickness); set => SetValue((int)EffectImpl.Properties.Thickness, value); }
        public BevelMode Mode { get => (BevelMode)GetIntValue((int)EffectImpl.Properties.Mode); set => SetValue((int)EffectImpl.Properties.Mode, (int)value); }

        [CustomEffect(2)]
        sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.SourceRect)]
            public Vector4 SourceRect
            {
                get => constants.SourceRect;
                set { constants.SourceRect = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Thickness)]
            public float Thickness
            {
                get => constants.Thickness;
                set { constants.Thickness = value; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.Mode)]
            public int Mode
            {
                get => constants.Mode;
                set { constants.Mode = value; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("BevelSdfResolve")) { }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                drawInfo.SetInputDescription(0, new InputDescription { Filter = Filter.MinMagMipPoint, LevelOfDetailCount = 1 });
                drawInfo.SetOutputBuffer(BufferPrecision.PerChannel16Float, ChannelDepth.Four);
            }

            protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(constants);

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public Vector4 SourceRect;
                public float Thickness;
                public int Mode;
            }

            public enum Properties
            {
                SourceRect,
                Thickness,
                Mode,
            }
        }
    }
}

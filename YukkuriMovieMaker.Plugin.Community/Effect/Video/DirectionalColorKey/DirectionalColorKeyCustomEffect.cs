using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey
{
    internal sealed class DirectionalColorKeyCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public Vector4 Cluster0
        {
            get => GetVector4Value((int)EffectImpl.Properties.Cluster0);
            set => SetValue((int)EffectImpl.Properties.Cluster0, value);
        }
        public Vector4 Cluster1
        {
            get => GetVector4Value((int)EffectImpl.Properties.Cluster1);
            set => SetValue((int)EffectImpl.Properties.Cluster1, value);
        }
        public Vector4 Cluster2
        {
            get => GetVector4Value((int)EffectImpl.Properties.Cluster2);
            set => SetValue((int)EffectImpl.Properties.Cluster2, value);
        }
        public Vector4 Cluster3
        {
            get => GetVector4Value((int)EffectImpl.Properties.Cluster3);
            set => SetValue((int)EffectImpl.Properties.Cluster3, value);
        }
        public Vector3 BackgroundLab
        {
            get => GetVector3Value((int)EffectImpl.Properties.BackgroundLab);
            set => SetValue((int)EffectImpl.Properties.BackgroundLab, value);
        }
        public float NoiseThreshold
        {
            get => GetFloatValue((int)EffectImpl.Properties.NoiseThreshold);
            set => SetValue((int)EffectImpl.Properties.NoiseThreshold, value);
        }
        public float SpillStrength
        {
            get => GetFloatValue((int)EffectImpl.Properties.SpillStrength);
            set => SetValue((int)EffectImpl.Properties.SpillStrength, value);
        }
        public float EdgeSoftness
        {
            get => GetFloatValue((int)EffectImpl.Properties.EdgeSoftness);
            set => SetValue((int)EffectImpl.Properties.EdgeSoftness, value);
        }
        public float DespillBias
        {
            get => GetFloatValue((int)EffectImpl.Properties.DespillBias);
            set => SetValue((int)EffectImpl.Properties.DespillBias, value);
        }
        public float OutputForeground
        {
            get => GetFloatValue((int)EffectImpl.Properties.OutputForeground);
            set => SetValue((int)EffectImpl.Properties.OutputForeground, value);
        }
        public Vector3 BackgroundChromaDir
        {
            get => GetVector3Value((int)EffectImpl.Properties.BackgroundChromaDir);
            set => SetValue((int)EffectImpl.Properties.BackgroundChromaDir, value);
        }
        public int ClusterCount
        {
            get => GetIntValue((int)EffectImpl.Properties.ClusterCount);
            set => SetValue((int)EffectImpl.Properties.ClusterCount, value);
        }
        public Vector3 BackgroundSrgb
        {
            get => GetVector3Value((int)EffectImpl.Properties.BackgroundSrgb);
            set => SetValue((int)EffectImpl.Properties.BackgroundSrgb, value);
        }
        public int DeviceInputWidth => GetIntValue((int)EffectImpl.Properties.DeviceInputWidth);
        public int DeviceInputHeight => GetIntValue((int)EffectImpl.Properties.DeviceInputHeight);

        [CustomEffect(2)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private ConstantBuffer _cb;

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.Cluster0)]
            public Vector4 Cluster0 { get => _cb.Cluster0; set { _cb.Cluster0 = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.Cluster1)]
            public Vector4 Cluster1 { get => _cb.Cluster1; set { _cb.Cluster1 = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.Cluster2)]
            public Vector4 Cluster2 { get => _cb.Cluster2; set { _cb.Cluster2 = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.Cluster3)]
            public Vector4 Cluster3 { get => _cb.Cluster3; set { _cb.Cluster3 = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector3, (int)Properties.BackgroundLab)]
            public Vector3 BackgroundLab { get => _cb.BackgroundLab; set { _cb.BackgroundLab = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.NoiseThreshold)]
            public float NoiseThreshold { get => _cb.NoiseThreshold; set { _cb.NoiseThreshold = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.SpillStrength)]
            public float SpillStrength { get => _cb.SpillStrength; set { _cb.SpillStrength = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.EdgeSoftness)]
            public float EdgeSoftness { get => _cb.EdgeSoftness; set { _cb.EdgeSoftness = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.DespillBias)]
            public float DespillBias { get => _cb.DespillBias; set { _cb.DespillBias = value; UpdateConstants(); } }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.OutputForeground)]
            public float OutputForeground { get => _cb.OutputForeground; set { _cb.OutputForeground = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector3, (int)Properties.BackgroundChromaDir)]
            public Vector3 BackgroundChromaDir { get => _cb.BackgroundChromaDir; set { _cb.BackgroundChromaDir = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.ClusterCount)]
            public int ClusterCount { get => _cb.ClusterCount; set { _cb.ClusterCount = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector3, (int)Properties.BackgroundSrgb)]
            public Vector3 BackgroundSrgb { get => _cb.BackgroundSrgb; set { _cb.BackgroundSrgb = value; UpdateConstants(); } }

            private int _deviceInputWidth;
            private int _deviceInputHeight;

            [CustomEffectProperty(PropertyType.Int32, (int)Properties.DeviceInputWidth)]
            public int DeviceInputWidth { get => _deviceInputWidth; set => _deviceInputWidth = value; }
            [CustomEffectProperty(PropertyType.Int32, (int)Properties.DeviceInputHeight)]
            public int DeviceInputHeight { get => _deviceInputHeight; set => _deviceInputHeight = value; }

            public EffectImpl() : base(ShaderResourceUri.Get("DirectionalColorKey")) { }

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
                if (inputRects.Length > 0)
                {
                    _deviceInputWidth = inputRects[0].Right - inputRects[0].Left;
                    _deviceInputHeight = inputRects[0].Bottom - inputRects[0].Top;
                }
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                for (int i = 0; i < inputRects.Length; i++)
                    inputRects[i] = outputRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public Vector4 Cluster0;
                public Vector4 Cluster1;
                public Vector4 Cluster2;
                public Vector4 Cluster3;
                public Vector3 BackgroundLab;
                public float NoiseThreshold;
                public float SpillStrength;
                public float EdgeSoftness;
                public float DespillBias;
                public float OutputForeground;
                public Vector3 BackgroundChromaDir;
                public int ClusterCount;
                public Vector3 BackgroundSrgb;
            }

            public enum Properties : int
            {
                Cluster0 = 0,
                Cluster1 = 1,
                Cluster2 = 2,
                Cluster3 = 3,
                BackgroundLab = 4,
                NoiseThreshold = 5,
                SpillStrength = 6,
                EdgeSoftness = 7,
                DespillBias = 8,
                OutputForeground = 9,
                BackgroundChromaDir = 10,
                ClusterCount = 11,
                BackgroundSrgb = 12,
                DeviceInputWidth = 13,
                DeviceInputHeight = 14,
            }
        }
    }
}

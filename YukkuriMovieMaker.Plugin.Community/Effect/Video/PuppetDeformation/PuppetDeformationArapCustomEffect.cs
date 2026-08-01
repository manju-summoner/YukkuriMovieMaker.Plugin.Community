using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    /// <summary>
    /// ARAP方式のパペット変形。CPU側で解いた変形後メッシュを
    /// D2Dカスタムエフェクトの頂点シェーダーでテクスチャ付き描画する。
    /// </summary>
    internal sealed class PuppetDeformationArapCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        /// <summary>描画できる最大三角形数</summary>
        public const int MaxTriangles = 5440;
        /// <summary>頂点バッファの最大頂点数（三角形リスト展開後）</summary>
        public const int MaxVertices = MaxTriangles * 3;
        /// <summary>頂点1つあたりのバイト数（変形後xy + レストxy）</summary>
        public const int VertexStride = 16;

        public float VertexCount
        {
            set => SetValue((int)EffectImpl.Properties.VertexCount, value);
            get => GetFloatValue((int)EffectImpl.Properties.VertexCount);
        }
        public float TightLocalLeft
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalLeft, value);
            get => GetFloatValue((int)EffectImpl.Properties.TightLocalLeft);
        }
        public float TightLocalTop
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalTop, value);
            get => GetFloatValue((int)EffectImpl.Properties.TightLocalTop);
        }
        public float TightLocalRight
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalRight, value);
            get => GetFloatValue((int)EffectImpl.Properties.TightLocalRight);
        }
        public float TightLocalBottom
        {
            set => SetValue((int)EffectImpl.Properties.TightLocalBottom, value);
            get => GetFloatValue((int)EffectImpl.Properties.TightLocalBottom);
        }

        /// <summary>
        /// 頂点データ（VertexStride × 頂点数）。
        /// 各頂点は float4 {変形後ローカルX, 変形後ローカルY, レストローカルX, レストローカルY}。
        /// </summary>
        public byte[] VertexData
        {
            set => SetValue((int)EffectImpl.Properties.VertexData, value);
        }

        protected override void DisposeCore(nint nativePointer, bool disposing)
        {
            //EffectImplの管理オブジェクトはD2Dの参照が切れてもGCまで生存するため、
            //保持している頂点バッファはエフェクト破棄時にプロパティ経由で確実に解放する
            if (disposing && IsEnabled)
                SetValue((int)EffectImpl.Properties.ReleaseResources, true);
            base.DisposeCore(nativePointer, disposing);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            const float MaxLocalExtent = 4096f;
            const int VertexBufferByteSize = MaxVertices * VertexStride;

            static readonly Guid GUID_VertexShader = Guid.NewGuid();

            ID2D1VertexBuffer? vertexBuffer;
            ConstantBuffer _cb;
            readonly byte[] _vertexData = new byte[VertexBufferByteSize];
            int _vertexCount;
            int _uploadedByteCount;
            bool _vertexDataDirty = true;
            float _tightLocalLeft, _tightLocalTop, _tightLocalRight, _tightLocalBottom;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.VertexCount)]
            public float VertexCount
            {
                get => _vertexCount;
                set
                {
                    _vertexCount = Math.Clamp((int)value, 0, MaxVertices);
                    UpdateConstants();
                }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalLeft)]
            public float TightLocalLeft { get => _tightLocalLeft; set => _tightLocalLeft = Math.Clamp(value, -MaxLocalExtent, MaxLocalExtent); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalTop)]
            public float TightLocalTop { get => _tightLocalTop; set => _tightLocalTop = Math.Clamp(value, -MaxLocalExtent, MaxLocalExtent); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalRight)]
            public float TightLocalRight { get => _tightLocalRight; set => _tightLocalRight = Math.Clamp(value, -MaxLocalExtent, MaxLocalExtent); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TightLocalBottom)]
            public float TightLocalBottom { get => _tightLocalBottom; set => _tightLocalBottom = Math.Clamp(value, -MaxLocalExtent, MaxLocalExtent); }

            [CustomEffectProperty(PropertyType.Bool, (int)Properties.ReleaseResources)]
            public bool ReleaseResources
            {
                get => vertexBuffer is null;
                set
                {
                    if (!value)
                        return;
                    vertexBuffer?.Dispose();
                    vertexBuffer = null;
                }
            }

            [CustomEffectProperty(PropertyType.Blob, (int)Properties.VertexData)]
            public byte[] VertexData
            {
                get => _vertexData;
                set
                {
                    if (value is null)
                        return;
                    var length = Math.Min(value.Length, _vertexData.Length);
                    Array.Copy(value, _vertexData, length);
                    //前回より短いデータが来た場合も、前回転送済みの範囲は上書きし直す
                    _uploadedByteCount = Math.Max(_uploadedByteCount, length);
                    _vertexDataDirty = true;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("PuppetDeformationArap"))
            {
            }

            public override void Initialize(ID2D1EffectContext effectContext, ID2D1TransformGraph transformGraph)
            {
                base.Initialize(effectContext, transformGraph);

                var vertexShaderBytes = PackResourceReader.ReadAllBytes(ShaderResourceUri.Get("PuppetDeformationArapVertex"));
                effectContext.LoadVertexShader(GUID_VertexShader, vertexShaderBytes, vertexShaderBytes.Length);

                //インスタンスごとに内容が異なるためresourceIdは指定せず共有しない
                vertexBuffer = effectContext.CreateVertexBuffer(
                    new VertexBufferProperties(1, VertexUsage.Dynamic, new byte[VertexBufferByteSize], VertexBufferByteSize),
                    null,
                    new CustomVertexBufferProperties(
                        vertexShaderBytes,
                        VertexStride,
                        new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
                        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0, 8)));
            }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                UpdateConstants();
            }

            protected override void UpdateConstants()
            {
                if (drawInformation is null || vertexBuffer is null)
                    return;

                if (_vertexDataDirty)
                {
                    vertexBuffer.Map(out var mapped, VertexBufferByteSize);
                    try
                    {
                        var copyBytes = Math.Clamp(_uploadedByteCount, 0, VertexBufferByteSize);
                        if (copyBytes > 0)
                            Marshal.Copy(_vertexData, 0, mapped, copyBytes);
                    }
                    finally
                    {
                        vertexBuffer.Unmap();
                    }
                    _vertexDataDirty = false;
                }

                drawInformation.SetVertexShaderConstantBuffer(in _cb);

                //頂点数0のVertexRangeはE_INVALIDARGになるため、頂点データが揃ってから設定する
                if (_vertexCount > 0)
                {
                    drawInformation.SetVertexProcessing(
                        vertexBuffer,
                        VertexOptions.None,
                        null,
                        new VertexRange(0, _vertexCount),
                        GUID_VertexShader);
                }
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);
                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    outputOpaqueSubRect = default;
                    return;
                }

                _cb.InputLeft = inputRect.Left;
                _cb.InputTop = inputRect.Top;
                _cb.InputWidth = inputRect.Right - inputRect.Left;
                _cb.InputHeight = inputRect.Bottom - inputRect.Top;
                UpdateConstants();

                if (_tightLocalRight > _tightLocalLeft && _tightLocalBottom > _tightLocalTop)
                {
                    float cx = inputRect.Left + _cb.InputWidth * 0.5f;
                    float cy = inputRect.Top + _cb.InputHeight * 0.5f;
                    int tl = (int)Math.Floor(cx + _tightLocalLeft);
                    int tt = (int)Math.Floor(cy + _tightLocalTop);
                    int tr = (int)Math.Ceiling(cx + _tightLocalRight);
                    int tb = (int)Math.Ceiling(cy + _tightLocalBottom);

                    outputRect = tr > tl && tb > tt
                        ? new RawRect(tl, tt, tr, tb)
                        : inputRect;
                }
                else
                {
                    outputRect = inputRect;
                }

                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                if (inputRects.Length > 0)
                    inputRects[0] = new RawRect(inputRect.Left - 2, inputRect.Top - 2, inputRect.Right + 2, inputRect.Bottom + 2);
            }

            protected override void DisposeCore(bool disposing)
            {
                if (disposing)
                {
                    vertexBuffer?.Dispose();
                    vertexBuffer = null;
                }
                base.DisposeCore(disposing);
            }

            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float InputLeft;
                public float InputTop;
                public float InputWidth;
                public float InputHeight;
            }

            public enum Properties : int
            {
                VertexCount = 0,
                TightLocalLeft = 1,
                TightLocalTop = 2,
                TightLocalRight = 3,
                TightLocalBottom = 4,
                VertexData = 5,
                ReleaseResources = 6,
            }
        }
    }
}

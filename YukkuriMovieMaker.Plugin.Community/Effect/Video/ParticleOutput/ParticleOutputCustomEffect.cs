using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ParticleOutput
{
    /// <summary>
    /// パーティクル出力エフェクトのD2Dカスタムエフェクト。
    /// CPU側（<see cref="ParticleOutputParticleBuilder"/>）が毎フレーム構築する
    /// 「生存中の粒子の発生時点の属性」を動的頂点バッファへ転送し、
    /// 経過時間に応じた運動は頂点シェーダーが計算する。
    /// 全パラメータのアニメーションに対応するため、粒子ごとの属性は定数ではなく頂点データで渡す。
    ///
    /// 頂点データはピン留め済みバッファの {ポインタ, バイト数, 世代カウンタ} だけをプロパティで渡し、
    /// GPUの頂点バッファへ直接コピーする（毎フレームのblob確保・多重コピーを避ける。CrashMeshと同じ方式）。
    ///
    /// 頂点バッファはInitializeでのみ生成できる（Initialize外でCreateVertexBufferを呼ぶと
    /// EndDrawがd2d1.dll内部で無限ループする）ため、バッファサイズはコンストラクタで確定し、
    /// 容量が足りなくなったら呼び出し側がエフェクトごと作り直す。
    /// </summary>
    internal sealed class ParticleOutputCustomEffect : D2D1CustomShaderEffectBase
    {
        /// <summary>同時に存在できる粒子の最大数</summary>
        public const int MaxParticles = 65536;
        /// <summary>最大頂点数（粒子 × 6頂点）</summary>
        public const int MaxVertices = MaxParticles * 6;
        /// <summary>頂点1つあたりのバイト数（コーナーxy + 属性float14個）</summary>
        public const int VertexStride = 64;
        /// <summary>頂点バッファの初期バイト数（1024粒子が収まる大きさ）</summary>
        public const int InitialVertexBufferByteSize = 1024 * 6 * VertexStride;

        /// <summary>頂点シェーダーへ渡す定数。レイアウトはParticleOutputVertex.hlslのConstantsと一致させること。</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ConstantBuffer
        {
            //c0: 入力範囲の中心（シーン座標）、入力範囲の半径(px)
            public Vector2 BoundsCenter;
            public Vector2 BoundsHalf;
        }

        //Initializeはbase呼び出し（CreateEffect）中に走るため、バッファサイズはコンストラクタ引数を
        //ThreadStatic経由でEffectImpl.Initializeへ渡す（CreateEffectは同一スレッドで同期実行される）
        [ThreadStatic]
        static int initializeVertexBufferByteSize;

        /// <summary>このエフェクトが保持する頂点バッファのバイト数。これを超える頂点データは描画できない。</summary>
        public int VertexBufferByteSize { get; }

        public ParticleOutputCustomEffect(IGraphicsDevicesAndContext devices, int vertexBufferByteSize)
            : base(CreateWithVertexBufferSize(devices, vertexBufferByteSize))
        {
            VertexBufferByteSize = vertexBufferByteSize;
        }

        static nint CreateWithVertexBufferSize(IGraphicsDevicesAndContext devices, int vertexBufferByteSize)
        {
            initializeVertexBufferByteSize = vertexBufferByteSize;
            return Create<EffectImpl>(devices);
        }

        public float VertexCount
        {
            set => SetValue((int)EffectImpl.Properties.VertexCount, value);
            get => GetFloatValue((int)EffectImpl.Properties.VertexCount);
        }
        public float DeformedLeft
        {
            set => SetValue((int)EffectImpl.Properties.DeformedLeft, value);
            get => GetFloatValue((int)EffectImpl.Properties.DeformedLeft);
        }
        public float DeformedTop
        {
            set => SetValue((int)EffectImpl.Properties.DeformedTop, value);
            get => GetFloatValue((int)EffectImpl.Properties.DeformedTop);
        }
        public float DeformedRight
        {
            set => SetValue((int)EffectImpl.Properties.DeformedRight, value);
            get => GetFloatValue((int)EffectImpl.Properties.DeformedRight);
        }
        public float DeformedBottom
        {
            set => SetValue((int)EffectImpl.Properties.DeformedBottom, value);
            get => GetFloatValue((int)EffectImpl.Properties.DeformedBottom);
        }
        /// <summary>trueで入力を点サンプリングする（補間方法がニアレストネイバーのとき用）</summary>
        public bool NearestNeighbor
        {
            set => SetValue((int)EffectImpl.Properties.NearestNeighbor, value);
            get => GetBoolValue((int)EffectImpl.Properties.NearestNeighbor);
        }

        long vertexDataGeneration;

        /// <summary>
        /// 頂点データの場所を設定する。
        /// pointerはピン留め済み（移動しない）メモリを指し、次に本メソッドが呼ばれるか
        /// エフェクトが破棄されるまで有効であり続けること。
        /// 頂点レイアウトは <see cref="ParticleOutputParticleBuilder"/> を参照。
        /// </summary>
        public void SetVertexData(nint pointer, int byteCount)
        {
            //ポインタ・バイト数が前回と同じでもバッファの中身は毎フレーム変わる。
            //D2DのSetValueは値が前回と同一だとsetter呼び出しを省略し、頂点バッファへのコピーが
            //走らなくなるため、世代カウンタを含めて記述子の値を毎回変化させる
            var descriptor = new byte[24];
            MemoryMarshal.Write(descriptor.AsSpan(0, 8), (long)pointer);
            MemoryMarshal.Write(descriptor.AsSpan(8, 8), (long)byteCount);
            MemoryMarshal.Write(descriptor.AsSpan(16, 8), ++vertexDataGeneration);
            SetValue((int)EffectImpl.Properties.VertexData, descriptor);
        }

        /// <summary>頂点シェーダーの定数を設定する</summary>
        public void SetConstants(in ConstantBuffer constants)
        {
            var bytes = new byte[Marshal.SizeOf<ConstantBuffer>()];
            MemoryMarshal.Write(bytes, constants);
            SetValue((int)EffectImpl.Properties.Constants, bytes);
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
            //AABBがintに収まらないほど飛散した場合の出力範囲クランプ（画面から十分離れているため見た目には影響しない）
            const float MaxDeformedExtent = 1 << 24;

            static readonly Guid GUID_VertexShader = Guid.NewGuid();

            ID2D1VertexBuffer? vertexBuffer;
            int vertexBufferByteSize;

            nint _dataPointer;
            int _dataByteCount;
            long _dataGeneration;
            int _uploadedByteCount;
            bool _vertexDataDirty;
            int _vertexCount;
            ConstantBuffer _constants;
            bool _nearestNeighbor;
            float _deformedLeft, _deformedTop, _deformedRight, _deformedBottom;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.VertexCount)]
            public float VertexCount
            {
                get => _vertexCount;
                set => _vertexCount = Math.Clamp((int)value, 0, MaxVertices);
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.DeformedLeft)]
            public float DeformedLeft { get => _deformedLeft; set => _deformedLeft = ClampExtent(value); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.DeformedTop)]
            public float DeformedTop { get => _deformedTop; set => _deformedTop = ClampExtent(value); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.DeformedRight)]
            public float DeformedRight { get => _deformedRight; set => _deformedRight = ClampExtent(value); }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.DeformedBottom)]
            public float DeformedBottom { get => _deformedBottom; set => _deformedBottom = ClampExtent(value); }

            [CustomEffectProperty(PropertyType.Bool, (int)Properties.NearestNeighbor)]
            public bool NearestNeighbor
            {
                get => _nearestNeighbor;
                set
                {
                    if (_nearestNeighbor == value)
                        return;
                    _nearestNeighbor = value;
                    ApplyInputDescription();
                }
            }

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
                    vertexBufferByteSize = 0;
                    _dataPointer = 0;
                    _dataByteCount = 0;
                }
            }

            /// <summary>
            /// 頂点データの場所（24バイト: long ポインタ + long バイト数 + long 世代カウンタ）。
            /// 世代カウンタはSetValueの同値スキップを避けるためのもので、中身は使用しない。
            /// ラッパーの <see cref="ParticleOutputCustomEffect.SetVertexData"/> から設定される。
            /// </summary>
            [CustomEffectProperty(PropertyType.Blob, (int)Properties.VertexData)]
            public byte[] VertexData
            {
                get
                {
                    var descriptor = new byte[24];
                    MemoryMarshal.Write(descriptor.AsSpan(0, 8), (long)_dataPointer);
                    MemoryMarshal.Write(descriptor.AsSpan(8, 8), (long)_dataByteCount);
                    MemoryMarshal.Write(descriptor.AsSpan(16, 8), _dataGeneration);
                    return descriptor;
                }
                set
                {
                    if (value is null || value.Length < 16)
                        return;
                    _dataPointer = (nint)MemoryMarshal.Read<long>(value.AsSpan(0, 8));
                    _dataByteCount = (int)MemoryMarshal.Read<long>(value.AsSpan(8, 8));
                    if (value.Length >= 24)
                        _dataGeneration = MemoryMarshal.Read<long>(value.AsSpan(16, 8));
                    _vertexDataDirty = true;
                    UpdateConstants();
                }
            }

            /// <summary>頂点シェーダーの定数（ConstantBufferをそのままバイト列にしたもの）</summary>
            [CustomEffectProperty(PropertyType.Blob, (int)Properties.Constants)]
            public byte[] Constants
            {
                get
                {
                    var bytes = new byte[Marshal.SizeOf<ConstantBuffer>()];
                    MemoryMarshal.Write(bytes, _constants);
                    return bytes;
                }
                set
                {
                    if (value is null || value.Length < Marshal.SizeOf<ConstantBuffer>())
                        return;
                    _constants = MemoryMarshal.Read<ConstantBuffer>(value);
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("ParticleOutput"))
            {
            }

            public override void Initialize(ID2D1EffectContext effectContext, ID2D1TransformGraph transformGraph)
            {
                base.Initialize(effectContext, transformGraph);

                var vertexShaderBytes = PackResourceReader.ReadAllBytes(ShaderResourceUri.Get("ParticleOutputVertex"));
                effectContext.LoadVertexShader(GUID_VertexShader, vertexShaderBytes, vertexShaderBytes.Length);

                //頂点バッファの生成はInitialize中のみ可能。サイズはラッパーのコンストラクタからThreadStatic経由で受け取る。
                vertexBufferByteSize = Math.Clamp(initializeVertexBufferByteSize, 6 * VertexStride, MaxVertices * VertexStride);

                //インスタンスごとに内容が異なるためresourceIdは指定せず共有しない。初期データは不要（data=[]）
                vertexBuffer = effectContext.CreateVertexBuffer(
                    new VertexBufferProperties(1, VertexUsage.Dynamic, [], vertexBufferByteSize),
                    null,
                    new CustomVertexBufferProperties(
                        vertexShaderBytes,
                        VertexStride,
                        new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
                        new InputElementDescription("TEXCOORD", 0, Format.R32G32B32A32_Float, 0, 8),
                        new InputElementDescription("TEXCOORD", 1, Format.R32G32B32A32_Float, 0, 24),
                        new InputElementDescription("TEXCOORD", 2, Format.R32G32B32A32_Float, 0, 40),
                        new InputElementDescription("TEXCOORD", 3, Format.R32G32_Float, 0, 56)));
            }

            public override void SetDrawInfo(ID2D1DrawInfo drawInfo)
            {
                base.SetDrawInfo(drawInfo);
                ApplyInputDescription();
                UpdateConstants();
            }

            void ApplyInputDescription()
            {
                drawInformation?.SetInputDescription(0, new InputDescription
                {
                    Filter = _nearestNeighbor ? Filter.MinMagMipPoint : Filter.MinMagMipLinear,
                    LevelOfDetailCount = 1,
                });
            }

            protected override unsafe void UpdateConstants()
            {
                if (drawInformation is null || vertexBuffer is null)
                    return;

                drawInformation.SetVertexShaderConstantBuffer(in _constants);

                if (_vertexDataDirty && _dataPointer != 0 && _dataByteCount > 0)
                {
                    vertexBuffer.Map(out var mapped, vertexBufferByteSize);
                    try
                    {
                        var copyBytes = Math.Min(_dataByteCount, vertexBufferByteSize);
                        Buffer.MemoryCopy((void*)_dataPointer, (void*)mapped, vertexBufferByteSize, copyBytes);
                        _uploadedByteCount = copyBytes;
                    }
                    finally
                    {
                        vertexBuffer.Unmap();
                    }
                    _vertexDataDirty = false;
                }

                //頂点数0のVertexRangeはE_INVALIDARGになるため、頂点データが揃ってから設定する。
                //転送済み範囲を超える頂点は未初期化データの描画になるため範囲をクランプする。
                var drawCount = Math.Min(_vertexCount, _uploadedByteCount / VertexStride);
                if (drawCount > 0)
                {
                    drawInformation.SetVertexProcessing(
                        vertexBuffer,
                        VertexOptions.None,
                        null,
                        new VertexRange(0, drawCount),
                        GUID_VertexShader);
                }
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                //粒子は入力画像全域からサンプリングするため、クランプせず全域を保持する
                inputRect = inputRects[0];

                //粒子群のAABBが出力範囲。まだAABB未設定の場合は入力範囲を返す
                if (_deformedRight > _deformedLeft && _deformedBottom > _deformedTop)
                {
                    outputRect = new RawRect(
                        (int)MathF.Floor(_deformedLeft),
                        (int)MathF.Floor(_deformedTop),
                        (int)MathF.Ceiling(_deformedRight),
                        (int)MathF.Ceiling(_deformedBottom));
                }
                else
                {
                    outputRect = inputRect;
                }
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                //サンプリング位置は入力画像全体にまたがる。粒子境界のバイリニアサンプリングが
                //端の外側テクセルを要求するため、1px広げてエッジのアーティファクトを防ぐ。
                if (inputRects.Length > 0)
                    inputRects[0] = new RawRect(
                        inputRect.Left - 1,
                        inputRect.Top - 1,
                        inputRect.Right + 1,
                        inputRect.Bottom + 1);
            }

            static float ClampExtent(float value)
                => float.IsNaN(value) ? 0f : Math.Clamp(value, -MaxDeformedExtent, MaxDeformedExtent);

            protected override void DisposeCore(bool disposing)
            {
                if (disposing)
                {
                    vertexBuffer?.Dispose();
                    vertexBuffer = null;
                }
                base.DisposeCore(disposing);
            }

            public enum Properties : int
            {
                Constants = 0,
                DeformedLeft = 1,
                DeformedTop = 2,
                DeformedRight = 3,
                DeformedBottom = 4,
                ReleaseResources = 5,
                NearestNeighbor = 6,
                VertexData = 7,
                VertexCount = 8,
            }
        }
    }
}

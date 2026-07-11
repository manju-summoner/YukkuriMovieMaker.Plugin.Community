using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Particlize
{
    /// <summary>
    /// 粒子化エフェクトのD2Dカスタムエフェクト。
    /// 静的な頂点バッファ（粒子中心＋コーナーオフセット）を頂点シェーダーでラスタライズし、
    /// 粒子の運動（消える順序・飛散・重力・揺らぎ・回転・縮小・フェード）は
    /// 毎フレーム更新する定数バッファだけで計算する。CPU側の毎フレームの頂点再計算・転送は不要。
    ///
    /// 頂点バッファはInitializeでのみ生成できる（Initialize外＝プロパティ設定時や描画コールバック中に
    /// CreateVertexBufferを呼ぶと、EndDrawがd2d1.dll内部で無限ループする）ため、
    /// 頂点データはコンストラクタで確定し、グリッドが変わったら呼び出し側がエフェクトごと作り直す。
    /// 頂点データの生成は <see cref="ParticlizeVertexBufferBuilder"/> を参照。
    /// </summary>
    internal sealed class ParticlizeCustomEffect : D2D1CustomShaderEffectBase
    {
        /// <summary>粒子分割の各軸の最大セル数</summary>
        public const int MaxCellsPerAxis = 1023;
        /// <summary>最大頂点数（1023×1023粒子 × 6頂点）</summary>
        public const int MaxVertices = MaxCellsPerAxis * MaxCellsPerAxis * 6;
        /// <summary>頂点1つあたりのバイト数（粒子中心xy + コーナーオフセットxy）</summary>
        public const int VertexStride = 16;

        /// <summary>頂点シェーダーへ渡す粒子運動の定数。レイアウトはParticlizeVertex.hlslのConstantsと一致させること。</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ConstantBuffer
        {
            //c0: 経過時間(s)、伝播期間(s)、1/寿命(1/s)、順序のばらつき(0-1)
            public float Time;
            public float DissolveSpan;
            public float LifetimeInv;
            public float Randomness;
            //c1: 消える方向の単位ベクトル、順序の最小値、1/順序の範囲
            public Vector2 DissolveDirection;
            public float OrderMin;
            public float OrderInvRange;
            //c2: 飛散速度ベクトル(px/s)、拡散速度(px/s)、重力加速度(px/s^2)
            public Vector2 ScatterVelocity;
            public float SpreadVelocity;
            public float Gravity;
            //c3: 揺らぎ振幅(px)、回転速度(rad/s)、縮小量(0-1)、フェード量(0-1)
            public float Turbulence;
            public float RotationSpeed;
            public float Shrink;
            public float Fade;
            //c4: 乱数シード＋予約
            public float Seed;
            public float Pad1, Pad2, Pad3;
        }

        //Initializeはbase呼び出し（CreateEffect）中に走るため、頂点データはコンストラクタ引数を
        //ThreadStatic経由でEffectImpl.Initializeへ渡す（CreateEffectは同一スレッドで同期実行される）
        [ThreadStatic]
        static byte[]? initializeVertexData;

        /// <summary>このエフェクトが保持する頂点数</summary>
        public int VertexCount { get; }

        public ParticlizeCustomEffect(IGraphicsDevicesAndContext devices, byte[] vertexData)
            : base(CreateWithVertexData(devices, vertexData))
        {
            VertexCount = vertexData.Length / VertexStride;
        }

        static nint CreateWithVertexData(IGraphicsDevicesAndContext devices, byte[] vertexData)
        {
            initializeVertexData = vertexData;
            try
            {
                return Create<EffectImpl>(devices);
            }
            finally
            {
                initializeVertexData = null;
            }
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

        /// <summary>粒子運動の定数を設定する（Timeが毎フレーム変わるため同値スキップは発生しない）</summary>
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
            int vertexCount;

            ConstantBuffer _constants;
            bool _nearestNeighbor;
            float _deformedLeft, _deformedTop, _deformedRight, _deformedBottom;

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
                }
            }

            /// <summary>粒子運動の定数（ConstantBufferをそのままバイト列にしたもの）</summary>
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

            public EffectImpl() : base(ShaderResourceUri.Get("Particlize"))
            {
            }

            public override void Initialize(ID2D1EffectContext effectContext, ID2D1TransformGraph transformGraph)
            {
                base.Initialize(effectContext, transformGraph);

                var vertexShaderBytes = PackResourceReader.ReadAllBytes(ShaderResourceUri.Get("ParticlizeVertex"));
                effectContext.LoadVertexShader(GUID_VertexShader, vertexShaderBytes, vertexShaderBytes.Length);

                //頂点バッファの生成はInitialize中のみ可能。粒子は静的なので初期データ付きのStaticで作り、以後書き換えない。
                //データはラッパーのコンストラクタからThreadStatic経由で受け取る。
                var vertexData = initializeVertexData ?? new byte[6 * VertexStride];
                vertexCount = Math.Clamp(vertexData.Length / VertexStride, 0, MaxVertices);

                //インスタンスごとに内容が異なるためresourceIdは指定せず共有しない
                vertexBuffer = effectContext.CreateVertexBuffer(
                    new VertexBufferProperties(1, VertexUsage.Static, vertexData, vertexData.Length),
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

            protected override void UpdateConstants()
            {
                if (drawInformation is null || vertexBuffer is null)
                    return;

                drawInformation.SetVertexShaderConstantBuffer(in _constants);

                //頂点数0のVertexRangeはE_INVALIDARGになるため、頂点がある場合のみ設定する
                if (vertexCount > 0)
                {
                    drawInformation.SetVertexProcessing(
                        vertexBuffer,
                        VertexOptions.None,
                        null,
                        new VertexRange(0, vertexCount),
                        GUID_VertexShader);
                }
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                //粒子は入力画像全域からサンプリングするため、クランプせず全域を保持する
                inputRect = inputRects[0];

                //飛散後の粒子群のAABBが出力範囲。まだAABB未設定の場合は入力範囲を返す
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
            }
        }
    }
}

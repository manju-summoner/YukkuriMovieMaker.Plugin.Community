using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.MotionBlur
{
    /// <summary>
    /// 1フレーム前の位置までの変位 delta(q) = q * M + d に沿ってブラーを掛けるエフェクト。
    /// M, d は現フレームのローカル座標系のアフィン変換（ブラー量乗算済み）としてCPU側で計算して渡す。
    /// </summary>
    internal sealed class MotionBlurCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float M11
        {
            set => SetValue((int)EffectImpl.Properties.M11, value);
            get => GetFloatValue((int)EffectImpl.Properties.M11);
        }
        public float M12
        {
            set => SetValue((int)EffectImpl.Properties.M12, value);
            get => GetFloatValue((int)EffectImpl.Properties.M12);
        }
        public float M21
        {
            set => SetValue((int)EffectImpl.Properties.M21, value);
            get => GetFloatValue((int)EffectImpl.Properties.M21);
        }
        public float M22
        {
            set => SetValue((int)EffectImpl.Properties.M22, value);
            get => GetFloatValue((int)EffectImpl.Properties.M22);
        }
        public float Dx
        {
            set => SetValue((int)EffectImpl.Properties.Dx, value);
            get => GetFloatValue((int)EffectImpl.Properties.Dx);
        }
        public float Dy
        {
            set => SetValue((int)EffectImpl.Properties.Dy, value);
            get => GetFloatValue((int)EffectImpl.Properties.Dy);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.M11)]
            public float M11
            {
                get => constants.M11;
                set
                {
                    constants.M11 = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.M12)]
            public float M12
            {
                get => constants.M12;
                set
                {
                    constants.M12 = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.M21)]
            public float M21
            {
                get => constants.M21;
                set
                {
                    constants.M21 = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.M22)]
            public float M22
            {
                get => constants.M22;
                set
                {
                    constants.M22 = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Dx)]
            public float Dx
            {
                get => constants.Dx;
                set
                {
                    constants.Dx = value;
                    UpdateConstants();
                }
            }
            [CustomEffectProperty(PropertyType.Float, (int)Properties.Dy)]
            public float Dy
            {
                get => constants.Dy;
                set
                {
                    constants.Dy = value;
                    UpdateConstants();
                }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("MotionBlur")) { }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(constants);
            }

            public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
            {
                var rect = inputRects[0];
                var expand = GetExpansion(rect);
                outputRect = new RawRect(rect.Left - expand, rect.Top - expand, rect.Right + expand, rect.Bottom + expand);
                outputOpaqueSubRect = default;
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                var expand = GetExpansion(outputRect);
                inputRects[0] = new RawRect(outputRect.Left - expand, outputRect.Top - expand, outputRect.Right + expand, outputRect.Bottom + expand);
            }

            /// <summary>
            /// 矩形内の変位ベクトルの最大長からブラーの広がり幅を求める。
            /// 変位はqのアフィン関数なので、最大値は必ず四隅のいずれかで取る。
            /// </summary>
            int GetExpansion(RawRect rect)
            {
                Span<Vector2> corners =
                [
                    new(rect.Left, rect.Top),
                    new(rect.Right, rect.Top),
                    new(rect.Left, rect.Bottom),
                    new(rect.Right, rect.Bottom),
                ];
                var max = 0f;
                foreach (var q in corners)
                {
                    var delta = new Vector2(
                        q.X * constants.M11 + q.Y * constants.M21 + constants.Dx,
                        q.X * constants.M12 + q.Y * constants.M22 + constants.Dy);
                    max = Math.Max(max, Math.Min(delta.Length(), 2000f));
                }
                //センター合わせでサンプリングするため広がりは変位の半分
                return (int)Math.Ceiling(max * 0.5f) + 1;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float M11;
                public float M12;
                public float M21;
                public float M22;
                public float Dx;
                public float Dy;
            }
            public enum Properties : int
            {
                M11 = 0,
                M12 = 1,
                M21 = 2,
                M22 = 3,
                Dx = 4,
                Dy = 5,
            }
        }
    }
}

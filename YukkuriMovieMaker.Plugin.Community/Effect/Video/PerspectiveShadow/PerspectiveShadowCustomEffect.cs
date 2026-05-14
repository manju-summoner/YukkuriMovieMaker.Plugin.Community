using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PerspectiveShadow
{
    internal sealed class PerspectiveShadowCustomEffect(IGraphicsDevicesAndContext devices)
        : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public float LightX
        {
            set => SetValue((int)EffectImpl.Properties.LightX, value);
            get => GetFloatValue((int)EffectImpl.Properties.LightX);
        }
        public float LightY
        {
            set => SetValue((int)EffectImpl.Properties.LightY, value);
            get => GetFloatValue((int)EffectImpl.Properties.LightY);
        }
        public float LightHeight
        {
            set => SetValue((int)EffectImpl.Properties.LightHeight, value);
            get => GetFloatValue((int)EffectImpl.Properties.LightHeight);
        }
        public float GroundY
        {
            set => SetValue((int)EffectImpl.Properties.GroundY, value);
            get => GetFloatValue((int)EffectImpl.Properties.GroundY);
        }
        public float Opacity
        {
            set => SetValue((int)EffectImpl.Properties.Opacity, value);
            get => GetFloatValue((int)EffectImpl.Properties.Opacity);
        }
        public float Falloff
        {
            set => SetValue((int)EffectImpl.Properties.Falloff, value);
            get => GetFloatValue((int)EffectImpl.Properties.Falloff);
        }
        public float BlurRadius
        {
            set => SetValue((int)EffectImpl.Properties.BlurRadius, value);
            get => GetFloatValue((int)EffectImpl.Properties.BlurRadius);
        }
        public float Spread
        {
            set => SetValue((int)EffectImpl.Properties.Spread, value);
            get => GetFloatValue((int)EffectImpl.Properties.Spread);
        }
        public Vector4 ShadowColor
        {
            set => SetValue((int)EffectImpl.Properties.ShadowColor, value);
            get => GetVector4Value((int)EffectImpl.Properties.ShadowColor);
        }
        public float AlphaThreshold
        {
            set => SetValue((int)EffectImpl.Properties.AlphaThreshold, value);
            get => GetFloatValue((int)EffectImpl.Properties.AlphaThreshold);
        }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            private const float MaxExpansionFactor = 10f;
            private const int MaxOutputExpand = 4096;

            private ConstantBuffer _cb;
            private float _groundYOffset;
            private float _resolvedGroundY;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.LightX)]
            public float LightX { get => _cb.LightX; set { _cb.LightX = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.LightY)]
            public float LightY { get => _cb.LightY; set { _cb.LightY = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.LightHeight)]
            public float LightHeight { get => _cb.LightHeight; set { _cb.LightHeight = Math.Max(value, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.GroundY)]
            public float GroundY { get => _groundYOffset; set { _groundYOffset = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Opacity)]
            public float Opacity { get => _cb.Opacity; set { _cb.Opacity = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Falloff)]
            public float Falloff { get => _cb.Falloff; set { _cb.Falloff = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.BlurRadius)]
            public float BlurRadius { get => _cb.BlurRadius; set { _cb.BlurRadius = Math.Max(value, 0f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Spread)]
            public float Spread { get => _cb.Spread; set { _cb.Spread = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Vector4, (int)Properties.ShadowColor)]
            public Vector4 ShadowColor { get => _cb.ShadowColor; set { _cb.ShadowColor = value; UpdateConstants(); } }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.AlphaThreshold)]
            public float AlphaThreshold { get => _cb.AlphaThreshold; set { _cb.AlphaThreshold = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }

            public EffectImpl() : base(ShaderResourceUri.Get("PerspectiveShadow"))
            {
            }

            protected override void UpdateConstants()
            {
                drawInformation?.SetPixelShaderConstantBuffer(_cb);
            }

            private const float MaxSpreadFactor = 3f;
            private const float Epsilon = 1e-4f;

            private float ComputeDynamicBlur(float shadowDist)
            {
                float expansion = MathF.Min(shadowDist / MathF.Max(_cb.LightHeight, 1f) * _cb.Spread, MaxSpreadFactor);
                return _cb.BlurRadius * (1f + expansion);
            }

            private Vector2 ProjectToGround(Vector2 Q)
            {
                float H = _cb.LightHeight;
                float G = _resolvedGroundY;
                float h = Math.Max(0f, G - Q.Y);
                float denom = H - h;

                float t;
                if (denom <= 0f)
                    t = MaxExpansionFactor;
                else
                    t = Math.Min(H / denom, MaxExpansionFactor);

                var light = new Vector2(_cb.LightX, _cb.LightY);
                float Sx = light.X + (Q.X - light.X) * t;
                float Sy = light.Y + (G - light.Y) * t;
                return new Vector2(Sx, Sy);
            }

            public override void MapInputRectsToOutputRect(
                RawRect[] inputRects,
                RawRect[] inputOpaqueSubRects,
                out RawRect outputRect,
                out RawRect outputOpaqueSubRect)
            {
                inputRect = ClampInputRect(inputRects[0]);
                if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                {
                    outputRect = inputRect;
                    outputOpaqueSubRect = default;
                    return;
                }

                _resolvedGroundY = inputRect.Bottom + _groundYOffset;
                _cb.GroundY = _resolvedGroundY;
                UpdateConstants();

                var corners = new[]
                {
                    new Vector2(inputRect.Left, inputRect.Top),
                    new Vector2(inputRect.Right, inputRect.Top),
                    new Vector2(inputRect.Left, inputRect.Bottom),
                    new Vector2(inputRect.Right, inputRect.Bottom),
                };

                float minX = inputRect.Left;
                float minY = inputRect.Top;
                float maxX = inputRect.Right;
                float maxY = inputRect.Bottom;
                float maxDynBlur = 0f;

                foreach (var c in corners)
                {
                    var S = ProjectToGround(c);
                    float shadowDist = Vector2.Distance(S, c);
                    maxDynBlur = Math.Max(maxDynBlur, ComputeDynamicBlur(shadowDist));

                    minX = Math.Min(minX, S.X);
                    minY = Math.Min(minY, S.Y);
                    maxX = Math.Max(maxX, S.X);
                    maxY = Math.Max(maxY, S.Y);
                }

                int blurMargin = (int)Math.Ceiling(maxDynBlur) + 2;

                outputRect = new RawRect(
                    Math.Max((int)Math.Floor(minX) - blurMargin, inputRect.Left - MaxOutputExpand),
                    Math.Max((int)Math.Floor(minY) - blurMargin, inputRect.Top - MaxOutputExpand),
                    Math.Min((int)Math.Ceiling(maxX) + blurMargin, inputRect.Right + MaxOutputExpand),
                    Math.Min((int)Math.Ceiling(maxY) + blurMargin, inputRect.Bottom + MaxOutputExpand)
                );
                outputOpaqueSubRect = default;
            }

            private Vector2 UnprojectFromShadow(Vector2 S, out bool valid)
            {
                valid = false;
                float denom = _resolvedGroundY - _cb.LightY;
                if (MathF.Abs(denom) < Epsilon)
                    return Vector2.Zero;

                float m = (S.Y - _cb.LightY) / denom;
                if (m < 1f)
                    return Vector2.Zero;

                float invM = 1f / m;
                float Px = _cb.LightX + (S.X - _cb.LightX) * invM;
                float Py = _resolvedGroundY - _cb.LightHeight * (1f - invM);

                if (Py > _resolvedGroundY)
                    return Vector2.Zero;

                valid = true;
                return new Vector2(Px, Py);
            }

            private void ExpandBoundsFromUnprojection(Vector2 point, ref float minX, ref float minY, ref float maxX, ref float maxY)
            {
                var Q = UnprojectFromShadow(point, out bool valid);
                if (!valid)
                    return;

                float shadowDist = Vector2.Distance(point, Q);
                float dynBlur = ComputeDynamicBlur(shadowDist);

                minX = Math.Min(minX, Q.X - dynBlur);
                minY = Math.Min(minY, Q.Y - dynBlur);
                maxX = Math.Max(maxX, Q.X + dynBlur);
                maxY = Math.Max(maxY, Q.Y + dynBlur);
            }

            public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
            {
                float minX = outputRect.Left;
                float minY = outputRect.Top;
                float maxX = outputRect.Right;
                float maxY = outputRect.Bottom;

                const int Subdivisions = 4;
                float stepX = (outputRect.Right - outputRect.Left) / (float)Subdivisions;
                float stepY = (outputRect.Bottom - outputRect.Top) / (float)Subdivisions;

                for (int iy = 0; iy <= Subdivisions; iy++)
                {
                    for (int ix = 0; ix <= Subdivisions; ix++)
                    {
                        if (ix > 0 && ix < Subdivisions && iy > 0 && iy < Subdivisions)
                            continue;

                        var point = new Vector2(
                            outputRect.Left + stepX * ix,
                            outputRect.Top + stepY * iy);
                        ExpandBoundsFromUnprojection(point, ref minX, ref minY, ref maxX, ref maxY);
                    }
                }

                var light = new Vector2(_cb.LightX, _cb.LightY);
                if (light.X >= outputRect.Left && light.X <= outputRect.Right &&
                    light.Y >= outputRect.Top && light.Y <= outputRect.Bottom)
                {
                    minX = Math.Min(minX, inputRect.Left);
                    minY = Math.Min(minY, inputRect.Top);
                    maxX = Math.Max(maxX, inputRect.Right);
                    maxY = Math.Max(maxY, inputRect.Bottom);
                }

                inputRects[0] = new RawRect(
                    (int)Math.Floor(minX) - 2,
                    (int)Math.Floor(minY) - 2,
                    (int)Math.Ceiling(maxX) + 2,
                    (int)Math.Ceiling(maxY) + 2);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ConstantBuffer
            {
                public float LightX;
                public float LightY;
                public float LightHeight;
                public float GroundY;

                public float Opacity;
                public float Falloff;
                public float BlurRadius;
                public float Spread;

                public Vector4 ShadowColor;

                public float AlphaThreshold;
                public float Pad0;
                public float Pad1;
                public float Pad2;
            }

            public enum Properties : int
            {
                LightX = 0,
                LightY = 1,
                LightHeight = 2,
                GroundY = 3,
                Opacity = 4,
                Falloff = 5,
                BlurRadius = 6,
                Spread = 7,
                ShadowColor = 8,
                AlphaThreshold = 9,
            }
        }
    }
}

using System.Collections.Generic;
using System.Numerics;
using System.Windows.Input;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldWarpEffectProcessor(IGraphicsDevicesAndContext devices, VectorFieldWarpEffect item) : VideoEffectProcessorBase(devices)
    {
        const int FloatsPerPoint = 8;
        const float PositionLimit = 65536f;

        readonly VectorFieldWarpEffect item = item;
        readonly float[] pointData = new float[VectorFieldWarpCustomEffect.MaxPoints * FloatsPerPoint];

        VectorFieldWarpCustomEffect? effect;
        bool isFirst = true;
        int pointCount;
        int integrationSteps;
        float amount;
        float maxDisplacement;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var amount = Sanitize(item.Amount.GetValue(frame, length, fps) / 100d, 0f, 1f, 0f);
            var maxDisplacement = Sanitize(item.MaxDisplacement.GetValue(frame, length, fps), 0f, VectorFieldWarpCustomEffect.MaxDisplacementLimit, 0f);
            var integrationSteps = Math.Clamp(item.IntegrationSteps, 1, VectorFieldWarpCustomEffect.MaxIntegrationSteps);
            var points = item.Points;
            var pointCount = 0;
            var pointDataChanged = false;
            var controllers = new List<VideoEffectController>(Math.Min(points.Count, VectorFieldWarpCustomEffect.MaxPoints));

            for (var index = 0; index < Math.Min(points.Count, VectorFieldWarpCustomEffect.MaxPoints); index++)
            {
                var point = points[index];
                var x = Sanitize(point.X.GetValue(frame, length, fps), -PositionLimit, PositionLimit, 0f);
                var y = Sanitize(point.Y.GetValue(frame, length, fps), -PositionLimit, PositionLimit, 0f);
                var radialStrength = Sanitize(point.RadialStrength.GetValue(frame, length, fps), -VectorFieldPoint.StrengthLimit, VectorFieldPoint.StrengthLimit, 0f);
                var vortexStrength = Sanitize(point.VortexStrength.GetValue(frame, length, fps), -VectorFieldPoint.StrengthLimit, VectorFieldPoint.StrengthLimit, 0f);
                var radius = Sanitize(point.Radius.GetValue(frame, length, fps), 1f, VectorFieldPoint.RadiusLimit, 1f);

                if (point.IsEnabled && (radialStrength != 0f || vortexStrength != 0f))
                {
                    var offset = pointCount * FloatsPerPoint;
                    SetPointData(offset, x, ref pointDataChanged);
                    SetPointData(offset + 1, y, ref pointDataChanged);
                    SetPointData(offset + 2, radialStrength, ref pointDataChanged);
                    SetPointData(offset + 3, vortexStrength, ref pointDataChanged);
                    SetPointData(offset + 4, radius, ref pointDataChanged);
                    SetPointData(offset + 5, 0f, ref pointDataChanged);
                    SetPointData(offset + 6, 0f, ref pointDataChanged);
                    SetPointData(offset + 7, 0f, ref pointDataChanged);
                    pointCount++;
                }

                controllers.Add(CreateController(point, x, y));
            }

            if (isFirst || this.pointCount != pointCount)
                effect.PointCount = pointCount;
            if (isFirst || this.integrationSteps != integrationSteps)
                effect.IntegrationSteps = integrationSteps;
            if (isFirst || this.amount != amount)
                effect.Amount = amount;
            if (isFirst || this.maxDisplacement != maxDisplacement)
                effect.MaxDisplacement = maxDisplacement;
            if (isFirst || pointDataChanged)
            {
                var bytes = new byte[pointData.Length * sizeof(float)];
                Buffer.BlockCopy(pointData, 0, bytes, 0, bytes.Length);
                effect.PointData = bytes;
            }

            isFirst = false;
            this.pointCount = pointCount;
            this.integrationSteps = integrationSteps;
            this.amount = amount;
            this.maxDisplacement = maxDisplacement;

            var description = effectDescription.DrawDescription;
            return description with { Controllers = [.. description.Controllers, .. controllers] };
        }

        VideoEffectController CreateController(VectorFieldPoint point, float x, float y)
        {
            var controllerPoint = new ControllerPoint(
                new Vector3(x, y, 0f),
                args =>
                {
                    if (!point.IsSelected)
                        SelectExclusively(point);
                    foreach (var selectedPoint in item.Points.Where(candidate => candidate.IsSelected))
                    {
                        selectedPoint.X.AddToEachValues(args.Delta.X);
                        selectedPoint.Y.AddToEachValues(args.Delta.Y);
                    }
                })
            {
                OnDragStart = args =>
                {
                    if (args.ModifierKeys.HasFlag(ModifierKeys.Control))
                        ToggleSelection(point);
                    else if (!point.IsSelected)
                        SelectExclusively(point);
                },
                IsSelected = point.IsSelected,
                Shape = VideoControllerPointShape.Circle
            };
            return new VideoEffectController(item, [controllerPoint]);
        }

        void SelectExclusively(VectorFieldPoint target)
        {
            foreach (var point in item.Points)
                point.IsSelected = ReferenceEquals(point, target);
        }

        void ToggleSelection(VectorFieldPoint target)
        {
            if (!target.IsSelected)
            {
                target.IsSelected = true;
                return;
            }

            if (item.Points.Any(point => !ReferenceEquals(point, target) && point.IsSelected))
                target.IsSelected = false;
        }

        void SetPointData(int index, float value, ref bool changed)
        {
            if (pointData[index] == value)
                return;
            pointData[index] = value;
            changed = true;
        }

        static float Sanitize(double value, float minimum, float maximum, float fallback)
        {
            if (!double.IsFinite(value))
                return fallback;
            return (float)Math.Clamp(value, minimum, maximum);
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new VectorFieldWarpCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }
            disposer.Collect(effect);

            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
            isFirst = true;
        }
    }
}

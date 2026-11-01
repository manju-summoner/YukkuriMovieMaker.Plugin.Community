using System.Numerics;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    internal static class PenCameraFinalizer
    {
        const float Epsilon = 1e-6f;

        public static DrawDescription Apply(DrawDescription drawDescription)
        {
            var hasBillboard = drawDescription.Billboard != BillboardMode.None;
            var hasPerspective = drawDescription.PerspectiveDistance.HasValue;
            if (!hasBillboard && !hasPerspective)
                return drawDescription;

            var camera = drawDescription.Camera;
            if (hasBillboard && TryCreateBillboardCorrection(camera, drawDescription.Billboard, out var correction))
            {
                var draw = drawDescription.Draw;
                var corrected = Matrix4x4.CreateTranslation(-draw) * correction * Matrix4x4.CreateTranslation(draw) * camera;
                if (IsFinite(corrected))
                    camera = corrected;
            }

            if (hasPerspective)
            {
                var corrected = camera * CreatePerspectiveCorrection(drawDescription.PerspectiveDistance.Value);
                if (IsFinite(corrected))
                    camera = corrected;
            }

            return drawDescription with
            {
                Camera = camera,
                PerspectiveDistance = null,
                Billboard = BillboardMode.None,
            };
        }

        static Matrix4x4 CreatePerspectiveCorrection(float distance)
        {
            if ((!float.IsFinite(distance) && !float.IsPositiveInfinity(distance)) || distance <= 0f)
                return Matrix4x4.Identity;

            var value = 0.001f - 1f / distance;
            if (!float.IsFinite(value))
                return Matrix4x4.Identity;

            var correction = Matrix4x4.Identity;
            correction.M34 = value;
            return correction;
        }

        static bool TryCreateBillboardCorrection(Matrix4x4 camera, BillboardMode mode, out Matrix4x4 correction)
        {
            correction = Matrix4x4.Identity;
            if (!IsFinite(camera))
                return false;

            var rotationOnly = new Matrix4x4(
                camera.M11, camera.M12, camera.M13, 0f,
                camera.M21, camera.M22, camera.M23, 0f,
                camera.M31, camera.M32, camera.M33, 0f,
                0f, 0f, 0f, 1f);
            var determinant = rotationOnly.GetDeterminant();
            if (!float.IsFinite(determinant)
                || determinant <= Epsilon
                || !Matrix4x4.Decompose(rotationOnly, out var scale, out var rotation, out _)
                || !IsFinite(scale)
                || MathF.Abs(scale.X) <= Epsilon
                || MathF.Abs(scale.Y) <= Epsilon
                || MathF.Abs(scale.Z) <= Epsilon
                || !IsFinite(rotation)
                || rotation.LengthSquared() <= Epsilon)
                return false;

            rotation = Quaternion.Normalize(rotation);
            var matrix = Matrix4x4.CreateFromQuaternion(rotation);
            switch (mode)
            {
                case BillboardMode.Spherical:
                    correction = Matrix4x4.Transpose(matrix);
                    break;
                case BillboardMode.Cylindrical:
                    var forward = new Vector2(matrix.M31, matrix.M33);
                    if (!IsFinite(forward) || forward.LengthSquared() <= Epsilon)
                        return true;
                    forward = Vector2.Normalize(forward);
                    correction = Matrix4x4.CreateRotationY(-MathF.Atan2(forward.X, forward.Y));
                    break;
                default:
                    return false;
            }
            return IsFinite(correction);
        }

        static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        static bool IsFinite(Vector3 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        static bool IsFinite(Quaternion value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);

        static bool IsFinite(Matrix4x4 value)
            => float.IsFinite(value.M11) && float.IsFinite(value.M12) && float.IsFinite(value.M13) && float.IsFinite(value.M14)
            && float.IsFinite(value.M21) && float.IsFinite(value.M22) && float.IsFinite(value.M23) && float.IsFinite(value.M24)
            && float.IsFinite(value.M31) && float.IsFinite(value.M32) && float.IsFinite(value.M33) && float.IsFinite(value.M34)
            && float.IsFinite(value.M41) && float.IsFinite(value.M42) && float.IsFinite(value.M43) && float.IsFinite(value.M44);
    }
}

using System.Numerics;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    /// <summary>
    /// 本体が効果チェーンの後で行うカメラの確定（ビルボード補正と透視距離の焼き込み）を先取りして再現する。
    /// 位置追従はアイテムの画素が画面のどこに出るかを写像に使うため、確定前のカメラで計算すると
    /// 透視距離やビルボードを設定する効果と組み合わせたときに参照位置がずれる。
    /// 本体側の CameraFinalizer は internal でプラグインから呼べない。
    /// </summary>
    internal static class ColorTransferCameraFinalizer
    {
        private const float BasePerspectiveDistance = 1000f;
        private const float Epsilon = 1e-6f;

        public static Matrix4x4 Apply(DrawDescription drawDescription)
        {
            var camera = drawDescription.Camera;

            if (drawDescription.Billboard is not BillboardMode.None
                && TryCreateBillboardCorrection(camera, drawDescription.Billboard, out var billboardCorrection))
            {
                var pivot = drawDescription.Draw;
                var candidate = Matrix4x4.CreateTranslation(-pivot)
                    * billboardCorrection
                    * Matrix4x4.CreateTranslation(pivot)
                    * camera;
                if (IsFinite(candidate))
                    camera = candidate;
            }

            if (drawDescription.PerspectiveDistance is float distance)
            {
                var candidate = camera * CreatePerspectiveCorrection(distance);
                if (IsFinite(candidate))
                    camera = candidate;
            }

            return camera;
        }

        private static Matrix4x4 CreatePerspectiveCorrection(float distance)
        {
            if ((!float.IsFinite(distance) && !float.IsPositiveInfinity(distance)) || distance <= 0)
                return Matrix4x4.Identity;

            var m34 = 1f / BasePerspectiveDistance - 1f / distance;
            if (!float.IsFinite(m34))
                return Matrix4x4.Identity;

            var result = Matrix4x4.Identity;
            result.M34 = m34;
            return result;
        }

        private static bool TryCreateBillboardCorrection(Matrix4x4 camera, BillboardMode mode, out Matrix4x4 correction)
        {
            correction = Matrix4x4.Identity;
            if (!IsFinite(camera))
                return false;

            //平行移動と射影項を除き、Cameraの線形成分だけから回転を取り出す
            var linear = new Matrix4x4(
                camera.M11, camera.M12, camera.M13, 0,
                camera.M21, camera.M22, camera.M23, 0,
                camera.M31, camera.M32, camera.M33, 0,
                0, 0, 0, 1);
            var determinant = linear.GetDeterminant();
            if (!float.IsFinite(determinant) || determinant <= Epsilon
                || !Matrix4x4.Decompose(linear, out var scale, out var rotation, out _)
                || !IsFinite(scale)
                || MathF.Abs(scale.X) <= Epsilon
                || MathF.Abs(scale.Y) <= Epsilon
                || MathF.Abs(scale.Z) <= Epsilon
                || !IsFinite(rotation)
                || rotation.LengthSquared() <= Epsilon)
                return false;

            rotation = Quaternion.Normalize(rotation);
            var rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);

            switch (mode)
            {
                case BillboardMode.Spherical:
                    correction = Matrix4x4.Transpose(rotationMatrix);
                    break;
                case BillboardMode.Cylindrical:
                    var forward = new Vector2(rotationMatrix.M31, rotationMatrix.M33);
                    if (!IsFinite(forward) || forward.LengthSquared() <= Epsilon)
                        return true;
                    forward = Vector2.Normalize(forward);
                    var yaw = MathF.Atan2(forward.X, forward.Y);
                    correction = Matrix4x4.CreateRotationY(-yaw);
                    break;
                default:
                    return false;
            }

            return IsFinite(correction);
        }

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        private static bool IsFinite(Vector3 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        private static bool IsFinite(Quaternion value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y)
                && float.IsFinite(value.Z) && float.IsFinite(value.W);

        private static bool IsFinite(Matrix4x4 value)
            => float.IsFinite(value.M11) && float.IsFinite(value.M12) && float.IsFinite(value.M13) && float.IsFinite(value.M14)
                && float.IsFinite(value.M21) && float.IsFinite(value.M22) && float.IsFinite(value.M23) && float.IsFinite(value.M24)
                && float.IsFinite(value.M31) && float.IsFinite(value.M32) && float.IsFinite(value.M33) && float.IsFinite(value.M34)
                && float.IsFinite(value.M41) && float.IsFinite(value.M42) && float.IsFinite(value.M43) && float.IsFinite(value.M44);
    }
}

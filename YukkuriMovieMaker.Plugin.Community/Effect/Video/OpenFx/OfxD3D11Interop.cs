using System;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.Direct3D11;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>D2Dビットマップの基底D3D11テクスチャをCUDA interop呼び出し中だけ公開する。</summary>
    internal static class OfxD3D11Interop
    {
        static int hasLoggedFailure;
#if DEBUG
        internal static bool ForceSurfaceFailureForTest { get; set; }
#endif

        public static bool WithResources(OfxEffectInstance instance, ID2D1Bitmap1 first, ID2D1Bitmap1 second, Func<nint, nint, bool> action)
        {
#if DEBUG
            if (ForceSurfaceFailureForTest)
            {
                instance.OnD3D11SurfaceUnavailable("テスト用D2D面取得失敗");
                return false;
            }
#endif
            if (!instance.CanUseD3D11Interop)
                return false;
            return WithResources(first, second, action, instance.OnD3D11SurfaceUnavailable);
        }

        public static bool WithResources(OfxEffectInstance instance, ID2D1Bitmap1 first, ID2D1Bitmap1 second, ID2D1Bitmap1 third, Func<nint, nint, nint, bool> action)
        {
#if DEBUG
            if (ForceSurfaceFailureForTest)
            {
                instance.OnD3D11SurfaceUnavailable("テスト用D2D面取得失敗");
                return false;
            }
#endif
            if (!instance.CanUseD3D11Interop)
                return false;
            return WithResources(first, second, third, action, instance.OnD3D11SurfaceUnavailable);
        }

        public static bool WithResource(OfxEffectInstance instance, ID2D1Bitmap1 bitmap, Func<nint, bool> action)
        {
#if DEBUG
            if (ForceSurfaceFailureForTest)
            {
                instance.OnD3D11SurfaceUnavailable("テスト用D2D面取得失敗");
                return false;
            }
#endif
            if (!instance.CanUseD3D11Interop)
                return false;
            return WithResource(bitmap, action, instance.OnD3D11SurfaceUnavailable);
        }

        /// <summary>
        /// bitmapの基底D3D11 resourceを破棄・差し替え前にCUDA登録cacheから外す。
        /// backend側もCOM参照を保持するため呼び忘れでuse-after-freeにはならないが、
        /// 所有者が把握できる通常経路ではここで即時に登録解除してサイズ変更時の滞留を防ぐ。
        /// </summary>
        public static void ReleaseResource(OfxEffectInstance? instance, ID2D1Bitmap1? bitmap)
        {
            if (instance is null || bitmap is null)
                return;
            try
            {
                using var surface = bitmap.Surface;
                using var texture = surface.QueryInterface<ID3D11Texture2D>();
                instance.ReleaseD3D11Resource(texture.NativePointer);
            }
            catch (SharpGenException e)
            {
                LogFailureOnce(e);
            }
        }

        // instanceなしのオーバーロードは、インスタンス単位の失敗ラッチを必要としないテスト・診断用。
        // 本番レンダーは必ずinstance付き入口を使い、失敗後のSurface/QI再試行を抑止する。
        public static bool WithResources(ID2D1Bitmap1 first, ID2D1Bitmap1 second, Func<nint, nint, bool> action)
            => WithResources(first, second, action, LogFailureOnce);

        static bool WithResources(ID2D1Bitmap1 first, ID2D1Bitmap1 second, Func<nint, nint, bool> action, Action<SharpGenException> onFailure)
        {
            try
            {
                using var firstSurface = first.Surface;
                using var firstTexture = firstSurface.QueryInterface<ID3D11Texture2D>();
                using var secondSurface = second.Surface;
                using var secondTexture = secondSurface.QueryInterface<ID3D11Texture2D>();
                return action(firstTexture.NativePointer, secondTexture.NativePointer);
            }
            catch (SharpGenException e)
            {
                onFailure(e);
                return false;
            }
        }

        // テスト・診断用。通常の3リソースレンダーはinstance付きオーバーロードを使う。
        public static bool WithResources(
            ID2D1Bitmap1 first,
            ID2D1Bitmap1 second,
            ID2D1Bitmap1 third,
            Func<nint, nint, nint, bool> action)
            => WithResources(first, second, third, action, LogFailureOnce);

        static bool WithResources(
            ID2D1Bitmap1 first,
            ID2D1Bitmap1 second,
            ID2D1Bitmap1 third,
            Func<nint, nint, nint, bool> action,
            Action<SharpGenException> onFailure)
        {
            try
            {
                using var firstSurface = first.Surface;
                using var firstTexture = firstSurface.QueryInterface<ID3D11Texture2D>();
                using var secondSurface = second.Surface;
                using var secondTexture = secondSurface.QueryInterface<ID3D11Texture2D>();
                using var thirdSurface = third.Surface;
                using var thirdTexture = thirdSurface.QueryInterface<ID3D11Texture2D>();
                return action(firstTexture.NativePointer, secondTexture.NativePointer, thirdTexture.NativePointer);
            }
            catch (SharpGenException e)
            {
                onFailure(e);
                return false;
            }
        }

        // テスト・診断用。通常の1リソースレンダーはinstance付きオーバーロードを使う。
        public static bool WithResource(ID2D1Bitmap1 bitmap, Func<nint, bool> action)
            => WithResource(bitmap, action, LogFailureOnce);

        static bool WithResource(ID2D1Bitmap1 bitmap, Func<nint, bool> action, Action<SharpGenException> onFailure)
        {
            try
            {
                using var surface = bitmap.Surface;
                using var texture = surface.QueryInterface<ID3D11Texture2D>();
                return action(texture.NativePointer);
            }
            catch (SharpGenException e)
            {
                onFailure(e);
                return false;
            }
        }

        static void LogFailureOnce(SharpGenException exception)
        {
            if (System.Threading.Interlocked.Exchange(ref hasLoggedFailure, 1) != 0)
                return;
            OfxHostLog.Info($"OpenFX用D3D11リソースを取得できないためCPU経路へ切り替えます。error={exception.Message}");
        }
    }
}

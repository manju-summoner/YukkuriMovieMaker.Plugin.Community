using System;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// クリップから供給する画像（clipGetImage が返す画像プロパティセットの実体）。
    /// ピクセル形式は RGBA float・下から上への行順（OFX標準の左下原点）。
    /// premultiplicationは既定でpremultiplied（出力画像はGetClipPreferencesの宣言に応じて
    /// <see cref="OfxEffectInstance"/> がプロパティを上書きする）。バッファはこのオブジェクトが所有する。
    /// </summary>
    internal sealed unsafe class OfxImage : OfxObject
    {
        void* data;

        public OfxPropertySet Props { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>OFX座標（左下原点）でのこの画像の左下位置</summary>
        public int OffsetX { get; }
        public int OffsetY { get; }

        public int RowBytes => Width * 4 * sizeof(float);
        public float* Data => (float*)data;

        public OfxImage(int width, int height, int offsetX, int offsetY, string uniqueIdentifier)
        {
            Width = width;
            Height = height;
            OffsetX = offsetX;
            OffsetY = offsetY;
            data = NativeMemory.AllocZeroed((nuint)((long)RowBytes * height));
            Props = new OfxPropertySet { DebugName = $"image({uniqueIdentifier})" };
            Props.SetString(OfxConstants.PropType, OfxConstants.TypeImage);
            Props.SetString(OfxConstants.ImageEffectPropPixelDepth, OfxConstants.BitDepthFloat);
            Props.SetString(OfxConstants.ImageEffectPropComponents, OfxConstants.ImageComponentRGBA);
            Props.SetString(OfxConstants.ImageEffectPropPreMultiplication, OfxConstants.ImagePreMultiplied);
            Props.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
            Props.SetDouble(OfxConstants.ImagePropPixelAspectRatio, 1);
            Props.SetPointer(OfxConstants.ImagePropData, (nint)data);
            Props.SetIntN(OfxConstants.ImagePropBounds, offsetX, offsetY, offsetX + width, offsetY + height);
            Props.SetIntN(OfxConstants.ImagePropRegionOfDefinition, offsetX, offsetY, offsetX + width, offsetY + height);
            Props.SetInt(OfxConstants.ImagePropRowBytes, RowBytes);
            Props.SetString(OfxConstants.ImagePropField, OfxConstants.ImageFieldNone);
            Props.SetString(OfxConstants.ImagePropUniqueIdentifier, uniqueIdentifier);
            // プール画像は使い回されるため、propReset で消えた値が永続しないよう既定値を確定しておく
            Props.SealDefaults();
        }

        public override void Dispose()
        {
            Props.Dispose();
            if (data is not null)
            {
                NativeMemory.Free(data);
                data = null;
            }
            base.Dispose();
        }
    }
}

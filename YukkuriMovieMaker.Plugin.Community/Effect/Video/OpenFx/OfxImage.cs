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
        sealed class CpuImageStorage : IOfxImageStorage
        {
            void* data;

            public nint DataPointer => (nint)data;
            public nint OpenCLImage => 0;
            public int RowBytes { get; }
            public bool IsCpuAccessible => true;

            public CpuImageStorage(int width, int height)
            {
                RowBytes = width * 4 * sizeof(float);
                data = NativeMemory.AllocZeroed((nuint)((long)RowBytes * height));
            }

            public void Dispose()
            {
                if (data is not null)
                {
                    NativeMemory.Free(data);
                    data = null;
                }
            }
        }

        readonly IOfxImageStorage storage;

        public OfxPropertySet Props { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>OFX座標（左下原点）でのこの画像の左下位置</summary>
        public int OffsetX { get; }
        public int OffsetY { get; }

        public int RowBytes => storage.RowBytes;
        public float* Data => storage.IsCpuAccessible
            ? (float*)storage.DataPointer
            : throw new InvalidOperationException("GPU画像のデータはCPUから直接参照できません。");
        public IOfxImageStorage Storage => storage;

        public OfxImage(int width, int height, int offsetX, int offsetY, string uniqueIdentifier)
            : this(width, height, offsetX, offsetY, uniqueIdentifier, new CpuImageStorage(width, height))
        {
        }

        public OfxImage(int width, int height, int offsetX, int offsetY, string uniqueIdentifier, IOfxImageStorage storage)
        {
            Width = width;
            Height = height;
            OffsetX = offsetX;
            OffsetY = offsetY;
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Props = new OfxPropertySet { DebugName = $"image({uniqueIdentifier})" };
            Props.SetString(OfxConstants.PropType, OfxConstants.TypeImage);
            Props.SetString(OfxConstants.ImageEffectPropPixelDepth, OfxConstants.BitDepthFloat);
            Props.SetString(OfxConstants.ImageEffectPropComponents, OfxConstants.ImageComponentRGBA);
            Props.SetString(OfxConstants.ImageEffectPropPreMultiplication, OfxConstants.ImagePreMultiplied);
            Props.SetDoubleN(OfxConstants.ImageEffectPropRenderScale, 1, 1);
            Props.SetDouble(OfxConstants.ImagePropPixelAspectRatio, 1);
            Props.SetPointer(OfxConstants.ImagePropData, storage.DataPointer);
            if (storage.OpenCLImage != 0)
                Props.SetPointer(OfxConstants.ImageEffectPropOpenCLImage, storage.OpenCLImage);
            Props.SetIntN(OfxConstants.ImagePropBounds, offsetX, offsetY, offsetX + width, offsetY + height);
            Props.SetIntN(OfxConstants.ImagePropRegionOfDefinition, offsetX, offsetY, offsetX + width, offsetY + height);
            // GPU画像でも常に存在する規格プロパティ。OpenCL Imageだけは値0になる。
            Props.SetInt(OfxConstants.ImagePropRowBytes, RowBytes);
            Props.SetString(OfxConstants.ImagePropField, OfxConstants.ImageFieldNone);
            Props.SetString(OfxConstants.ImagePropUniqueIdentifier, uniqueIdentifier);
            // プール画像は使い回されるため、propReset で消えた値が永続しないよう既定値を確定しておく
            Props.SealDefaults();
        }

        public override void Dispose()
        {
            Props.Dispose();
            storage.Dispose();
            base.Dispose();
        }
    }
}

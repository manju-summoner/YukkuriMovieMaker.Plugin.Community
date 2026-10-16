using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation.Arap
{
    /// <summary>
    /// 入力の不透明な画素のうち、ARAPメッシュ（レスト位置）の三角形に覆われていないものがあるかを調べる。
    /// メッシュは構築時の入力のアルファから作るため、入力の中身が後から動くとメッシュの外へはみ出した部分が描かれず欠ける。
    /// 引き算は等倍のままGPUで行い（入力からメッシュの被覆をDestinationOutで消す）、
    /// 残りを膨張させてから縮小して読み戻すことで、1画素のはみ出しも縮小で消えずに検知できる。
    /// </summary>
    internal sealed class ArapMeshEscapeDetector : IDisposable
    {
        //読み戻す画像の長辺の目安(px)。転送量より読み戻しの同期待ちが支配的なので、これ以上小さくしても速くならない
        const int ReadbackTargetSize = 512;
        //Morphologyの幅の上限(100)に収まる縮小率の上限
        const int MaxDownscale = 49;
        //入力の範囲が毎フレーム変わっても読み戻し用ビットマップを作り直さないよう、この単位で大きめに確保する
        const int ReadbackAllocationUnit = 64;

        readonly ID2D1DeviceContext deviceContext;
        readonly PuppetDeformationArapCustomEffect coverage;
        readonly Vortice.Direct2D1.Effects.Flood opaqueFlood;
        readonly Vortice.Direct2D1.Effects.Crop coverageSource;
        readonly Vortice.Direct2D1.Effects.AffineTransform2D acceptedPlacement;
        readonly Vortice.Direct2D1.Effects.Composite coverageUnion;
        readonly Vortice.Direct2D1.Effects.Composite subtract;
        readonly Vortice.Direct2D1.Effects.Morphology dilate;

        IArapMesh? coverageMesh;
        Vector2 coverageShift;
        byte[]? coverageVertexData;

        //メッシュ構築直後から覆えていない場所。輪郭の簡略化で縁の画素がメッシュの外に残ることがあり、
        //そこを毎回はみ出しとみなすと、同じ入力から同じメッシュを作り直し続けてしまう。
        //メッシュを置き直しても一緒に動くよう、メッシュ座標で持つ
        IArapMesh? acceptedMesh;
        ID2D1Bitmap1? acceptedMask;
        Vector2 acceptedMaskOrigin;

        ID2D1Bitmap1? gpuBitmap;
        ID2D1Bitmap1? cpuBitmap;
        SizeI readbackCapacity;

        public ArapMeshEscapeDetector(IGraphicsDevicesAndContext devices)
        {
            deviceContext = devices.DeviceContext;
            coverage = new PuppetDeformationArapCustomEffect(devices);
            opaqueFlood = new Vortice.Direct2D1.Effects.Flood(deviceContext) { Color = new Vector4(1f, 1f, 1f, 1f) };
            coverageSource = new Vortice.Direct2D1.Effects.Crop(deviceContext);
            acceptedPlacement = new Vortice.Direct2D1.Effects.AffineTransform2D(deviceContext)
            {
                InterPolationMode = AffineTransform2DInterpolationMode.NearestNeighbor,
                BorderMode = BorderMode.Hard,
            };
            coverageUnion = new Vortice.Direct2D1.Effects.Composite(deviceContext) { InputCount = 1 };
            subtract = new Vortice.Direct2D1.Effects.Composite(deviceContext) { Mode = CompositeMode.DestinationOut };
            dilate = new Vortice.Direct2D1.Effects.Morphology(deviceContext) { Mode = MorphologyMode.Dilate };

            using (var floodOutput = opaqueFlood.Output)
                coverageSource.SetInput(0, floodOutput, true);
            //被覆はメッシュをレスト位置のまま不透明な面に描いたもの。入力の範囲で切り抜いた面を入力にして、エフェクトの原点を入力の中央に揃える
            using (var coverageSourceOutput = coverageSource.Output)
                coverage.SetInput(0, coverageSourceOutput, true);
            using (var coverageOutput = coverage.Output)
                coverageUnion.SetInput(0, coverageOutput, true);
            using (var unionOutput = coverageUnion.Output)
                subtract.SetInput(1, unionOutput, true);
            using (var subtractOutput = subtract.Output)
                dilate.SetInput(0, subtractOutput, true);
        }

        /// <summary>
        /// メッシュを構築した直後に呼び、そのメッシュで覆えていない場所を記録する。以後のはみ出し判定ではそこを除く。
        /// </summary>
        public unsafe void AcceptUncovered(ID2D1Image input, Vortice.RawRectF inputBounds, IArapMesh mesh, Vector2 meshToInputLocal)
        {
            ClearAccepted();
            acceptedMesh = mesh;

            //縮小すると記録が膨張とセルの分だけ太り、その帯へのはみ出しを見落とす。構築時だけなので等倍で読み戻す
            const int downscale = 1;
            var (cellLeft, cellTop, size) = GetCellRange(inputBounds, downscale);
            if (size.Width <= 0 || size.Height <= 0)
                return;

            using var gpu = CreateReadbackBitmap(size, BitmapOptions.Target);
            using var cpu = CreateReadbackBitmap(size, BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
            Scan(input, inputBounds, mesh, meshToInputLocal, downscale, cellLeft, cellTop, size, gpu, cpu);

            //メッシュを置き直したときの画素の丸めでずれても外れないよう、入力の範囲の外側も含めて1画素広げておく。
            //記録はメッシュの作り直しまで保持するため、入力全体ではなく覆えていない場所を囲む範囲だけを持つ
            byte[] grown;
            SizeI grownSize;
            int grownLeft, grownTop;
            var map = cpu.Map(MapOptions.Read);
            try
            {
                var ptr = (byte*)map.Bits;
                int minX = size.Width, minY = size.Height, maxX = -1, maxY = -1;
                for (var y = 0; y < size.Height; y++)
                {
                    var row = ptr + (long)y * map.Pitch;
                    for (var x = 0; x < size.Width; x++)
                    {
                        if (row[x] == 0)
                            continue;
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
                if (maxX < 0)
                    return;

                grownLeft = minX - 1;
                grownTop = minY - 1;
                grownSize = new SizeI(maxX - minX + 3, maxY - minY + 3);
                grown = new byte[grownSize.Width * grownSize.Height];
                for (var y = minY; y <= maxY; y++)
                {
                    var row = ptr + (long)y * map.Pitch;
                    for (var x = minX; x <= maxX; x++)
                    {
                        if (row[x] == 0)
                            continue;
                        for (var dy = 0; dy < 3; dy++)
                            for (var dx = 0; dx < 3; dx++)
                                grown[(y - grownTop + dy - 1) * grownSize.Width + (x - grownLeft + dx - 1)] = 255;
                    }
                }
            }
            finally
            {
                cpu.Unmap();
            }

            fixed (byte* data = grown)
            {
                acceptedMask = deviceContext.CreateBitmap(grownSize, (nint)data, grownSize.Width, new BitmapProperties1(
                    new PixelFormat(Format.A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    deviceContext.Dpi.Width,
                    deviceContext.Dpi.Height,
                    BitmapOptions.None));
            }
            //記録の左上をメッシュ原点（構築時の入力中央）からの位置で持つ
            acceptedMaskOrigin = new Vector2(cellLeft + grownLeft, cellTop + grownTop) - (BoundsCenter(inputBounds) + meshToInputLocal);
        }

        /// <summary>
        /// 入力の不透明な画素が、メッシュにもメッシュ構築直後から覆えていなかった場所にも含まれない位置にあればtrueを返す。
        /// </summary>
        /// <param name="input">パペット変形の入力</param>
        /// <param name="inputBounds">入力のローカル範囲</param>
        /// <param name="mesh">現在のメッシュ（座標はメッシュの原点からの位置）</param>
        /// <param name="meshToInputLocal">メッシュ座標から現在の入力中央を原点とする座標への移動量</param>
        public unsafe bool HasContentOutsideMesh(ID2D1Image input, Vortice.RawRectF inputBounds, IArapMesh mesh, Vector2 meshToInputLocal)
        {
            try
            {
                if (acceptedMask is not null && ReferenceEquals(acceptedMesh, mesh))
                {
                    var meshOrigin = BoundsCenter(inputBounds) + meshToInputLocal;
                    acceptedPlacement.TransformMatrix = Matrix3x2.CreateTranslation(meshOrigin + acceptedMaskOrigin);
                    acceptedPlacement.SetInput(0, acceptedMask, true);
                    coverageUnion.InputCount = 2;
                    using var placed = acceptedPlacement.Output;
                    coverageUnion.SetInput(1, placed, true);
                }

                return ScanDownscaled(input, inputBounds, mesh, meshToInputLocal);
            }
            finally
            {
                if (coverageUnion.InputCount == 2)
                {
                    coverageUnion.SetInput(1, null, true);
                    coverageUnion.InputCount = 1;
                }
                acceptedPlacement.SetInput(0, null, true);
            }
        }

        /// <summary>
        /// 入力に不透明な画素が1つでもあればtrueを返す。完全に透明な入力から作ったメッシュは範囲全体を覆うため、
        /// 中身が現れたことをはみ出しでは検知できない。その間の毎フレームの判定を、はみ出しの判定と同じ縮小した読み戻しで済ませる
        /// </summary>
        public bool HasOpaqueContent(ID2D1Image input, Vortice.RawRectF inputBounds)
            => ScanDownscaled(input, inputBounds, null, Vector2.Zero);

        /// <param name="mesh">被覆を引くメッシュ。nullなら入力をそのまま調べる</param>
        unsafe bool ScanDownscaled(ID2D1Image input, Vortice.RawRectF inputBounds, IArapMesh? mesh, Vector2 meshToInputLocal)
        {
            var downscale = ComputeDownscale(inputBounds);
            var (cellLeft, cellTop, size) = GetCellRange(inputBounds, downscale);
            if (size.Width <= 0 || size.Height <= 0)
                return false;
            EnsureReadbackBitmaps(size);
            Scan(input, inputBounds, mesh, meshToInputLocal, downscale, cellLeft, cellTop, size, gpuBitmap!, cpuBitmap!);

            var map = cpuBitmap!.Map(MapOptions.Read);
            try
            {
                var ptr = (byte*)map.Bits;
                for (var y = 0; y < size.Height; y++)
                {
                    var row = ptr + (long)y * map.Pitch;
                    for (var x = 0; x < size.Width; x++)
                    {
                        if (row[x] != 0)
                            return true;
                    }
                }
                return false;
            }
            finally
            {
                cpuBitmap.Unmap();
            }
        }

        static Vector2 BoundsCenter(Vortice.RawRectF bounds)
            => new((bounds.Left + bounds.Right) * 0.5f, (bounds.Top + bounds.Bottom) * 0.5f);

        static int ComputeDownscale(Vortice.RawRectF inputBounds)
        {
            var longSide = MathF.Max(inputBounds.Right - inputBounds.Left, inputBounds.Bottom - inputBounds.Top);
            return Math.Clamp((int)MathF.Ceiling(longSide / ReadbackTargetSize), 1, MaxDownscale);
        }

        /// <summary>
        /// 読み戻すセルの範囲を求める。セルの格子はアイテム座標の原点に揃え、範囲が画素の途中で切れていても端の画素を含むよう外側に広げる。
        /// </summary>
        static (int CellLeft, int CellTop, SizeI Size) GetCellRange(Vortice.RawRectF inputBounds, int downscale)
        {
            var cellLeft = (int)MathF.Floor(inputBounds.Left / downscale);
            var cellTop = (int)MathF.Floor(inputBounds.Top / downscale);
            var cellRight = (int)MathF.Ceiling(inputBounds.Right / downscale);
            var cellBottom = (int)MathF.Ceiling(inputBounds.Bottom / downscale);
            return (cellLeft, cellTop, new SizeI(cellRight - cellLeft, cellBottom - cellTop));
        }

        /// <summary>
        /// 入力のうち被覆に含まれない画素を、downscale角のセルに縮小してcpuの左上(size分)へ読み戻す。meshがnullなら被覆を引かない。
        /// </summary>
        void Scan(ID2D1Image input, Vortice.RawRectF inputBounds, IArapMesh? mesh, Vector2 meshToInputLocal, int downscale, int cellLeft, int cellTop, SizeI size, ID2D1Bitmap1 gpu, ID2D1Bitmap1 cpu)
        {
            //縮小時は各画素がセル中央の1点しか見ないため、セル内のどこにある残りも中央へ届く幅まで膨張させる
            var dilateSize = downscale == 1 ? 1 : downscale * 2 + 1;
            dilate.Width = dilateSize;
            dilate.Height = dilateSize;

            if (mesh is null)
            {
                dilate.SetInput(0, input, true);
            }
            else
            {
                UpdateCoverage(mesh, meshToInputLocal);
                coverageSource.Rectangle = new Vector4(inputBounds.Left, inputBounds.Top, inputBounds.Right, inputBounds.Bottom);
                subtract.SetInput(0, input, true);
            }
            var previousTarget = deviceContext.Target;
            var previousTransform = deviceContext.Transform;
            try
            {
                deviceContext.Target = gpu;
                deviceContext.Transform =
                    Matrix3x2.CreateTranslation(-cellLeft * downscale, -cellTop * downscale)
                    * Matrix3x2.CreateScale(1f / downscale);
                deviceContext.BeginDraw();
                deviceContext.Clear(new Color4(0f, 0f, 0f, 0f));
                using (var dilateOutput = dilate.Output)
                    deviceContext.DrawImage(dilateOutput, null, null, InterpolationMode.NearestNeighbor, CompositeMode.SourceCopy);
                deviceContext.EndDraw().CheckError();
            }
            finally
            {
                deviceContext.Transform = previousTransform;
                deviceContext.Target = previousTarget;
                previousTarget?.Dispose();
                if (mesh is null)
                {
                    using var subtractOutput = subtract.Output;
                    dilate.SetInput(0, subtractOutput, true);
                }
                else
                {
                    subtract.SetInput(0, null, true);
                }
            }

            cpu.CopyFromBitmap(new Int2(0, 0), gpu, new Vortice.RawRect(0, 0, size.Width, size.Height));
        }

        void UpdateCoverage(IArapMesh mesh, Vector2 meshToInputLocal)
        {
            if (ReferenceEquals(coverageMesh, mesh) && coverageShift == meshToInputLocal)
                return;

            var triangleIndices = mesh.TriangleIndices;
            var rests = mesh.RestPositions;
            var byteCount = triangleIndices.Length * PuppetDeformationArapCustomEffect.VertexStride;
            if (coverageVertexData is null || coverageVertexData.Length != byteCount)
                coverageVertexData = new byte[byteCount];
            var floats = MemoryMarshal.Cast<byte, float>(coverageVertexData.AsSpan());
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var o = 0;
            foreach (var v in triangleIndices)
            {
                var p = rests[v] + meshToInputLocal;
                //変形後位置もレスト位置にして、メッシュが覆う範囲をそのまま描く
                floats[o++] = p.X;
                floats[o++] = p.Y;
                floats[o++] = p.X;
                floats[o++] = p.Y;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            const float margin = 1f;
            coverage.VertexData = coverageVertexData;
            coverage.VertexCount = triangleIndices.Length;
            coverage.TightLocalLeft = minX - margin;
            coverage.TightLocalTop = minY - margin;
            coverage.TightLocalRight = maxX + margin;
            coverage.TightLocalBottom = maxY + margin;

            coverageMesh = mesh;
            coverageShift = meshToInputLocal;
        }

        void EnsureReadbackBitmaps(SizeI size)
        {
            if (gpuBitmap is not null && cpuBitmap is not null && readbackCapacity.Width >= size.Width && readbackCapacity.Height >= size.Height)
                return;

            var capacity = new SizeI(
                RoundUp(Math.Max(size.Width, readbackCapacity.Width), ReadbackAllocationUnit),
                RoundUp(Math.Max(size.Height, readbackCapacity.Height), ReadbackAllocationUnit));
            //確保に失敗したときに破棄済みのビットマップを使い回さないよう、先に手放す
            gpuBitmap?.Dispose();
            cpuBitmap?.Dispose();
            gpuBitmap = null;
            cpuBitmap = null;
            readbackCapacity = default;
            gpuBitmap = CreateReadbackBitmap(capacity, BitmapOptions.Target);
            cpuBitmap = CreateReadbackBitmap(capacity, BitmapOptions.CpuRead | BitmapOptions.CannotDraw);
            readbackCapacity = capacity;
        }

        ID2D1Bitmap1 CreateReadbackBitmap(SizeI size, BitmapOptions options)
            => deviceContext.CreateBitmap(size, new BitmapProperties1(
                new PixelFormat(Format.A8_UNorm, Vortice.DCommon.AlphaMode.Straight),
                deviceContext.Dpi.Width,
                deviceContext.Dpi.Height,
                options));

        static int RoundUp(int value, int unit) => (value + unit - 1) / unit * unit;

        /// <summary>
        /// 記録した覆えていない場所を捨てる。はみ出しを調べないメッシュへ作り直したときに古い記録を残さないために呼ぶ。
        /// </summary>
        public void ClearAccepted()
        {
            acceptedMask?.Dispose();
            acceptedMask = null;
            acceptedMesh = null;
        }

        public void Dispose()
        {
            dilate.SetInput(0, null, true);
            subtract.SetInput(0, null, true);
            subtract.SetInput(1, null, true);
            coverageUnion.SetInput(0, null, true);
            acceptedPlacement.SetInput(0, null, true);
            coverage.SetInput(0, null, true);
            coverageSource.SetInput(0, null, true);

            dilate.Dispose();
            subtract.Dispose();
            coverageUnion.Dispose();
            acceptedPlacement.Dispose();
            coverage.Dispose();
            coverageSource.Dispose();
            opaqueFlood.Dispose();
            gpuBitmap?.Dispose();
            cpuBitmap?.Dispose();
            gpuBitmap = null;
            cpuBitmap = null;
            coverageMesh = null;
            coverageVertexData = null;
            ClearAccepted();
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.DXGI;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using MapFlags = Vortice.Direct3D11.MapFlags;

namespace YukkuriMovieMaker.Plugin.Community.Commons.Compute
{
    internal sealed class ComputeBuffer<T> : IDisposable where T : unmanaged
    {
        readonly DisposeCollector disposer = new();
        readonly ComputeShaderDevice device;
        readonly ID3D11UnorderedAccessView? uav;
        readonly int stride = Unsafe.SizeOf<T>();
        readonly bool writable;
        ID3D11UnorderedAccessView? partialUav;
        ID3D11Buffer? staging;
        int partialCount = -1;
        bool disposed;

        public int Length { get; }
        public ID3D11Buffer Buffer { get; }
        public ID3D11ShaderResourceView Srv { get; }
        public ID3D11UnorderedAccessView Uav
            => uav ?? throw new InvalidOperationException("書き込み不可の領域に順不同アクセスビューはありません。");

        public ComputeBuffer(ComputeShaderDevice device, int length, bool writable)
        {
            this.device = device;
            this.writable = writable;
            Length = length;

            var description = new BufferDescription(
                stride * length,
                writable ? BindFlags.ShaderResource | BindFlags.UnorderedAccess : BindFlags.ShaderResource,
                writable ? ResourceUsage.Default : ResourceUsage.Dynamic,
                writable ? CpuAccessFlags.None : CpuAccessFlags.Write,
                ResourceOptionFlags.BufferStructured,
                stride);

            Buffer = device.Device.CreateBuffer(description, (SubresourceData?)null);
            disposer.Collect(Buffer);

            Srv = device.Device.CreateShaderResourceView(
                Buffer, new ShaderResourceViewDescription(Buffer, Format.Unknown, 0, length));
            disposer.Collect(Srv);

            if (writable)
            {
                uav = device.Device.CreateUnorderedAccessView(
                    Buffer, new UnorderedAccessViewDescription(Buffer, Format.Unknown, 0, length));
                disposer.Collect(uav);
            }
        }

        // WriteDiscard は Dynamic 専用のため、Default へは部分更新で送る。
        // https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_map
        // ("The resource must have been created with write access and dynamic usage")
        public unsafe void Upload(ReadOnlySpan<T> source)
        {
            var count = Math.Min(source.Length, Length);
            if (count <= 0)
                return;

            using var scope = device.Enter();

            if (!writable)
            {
                var mapped = device.Context.Map(Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
                try
                {
                    var destination = new Span<T>((void*)mapped.DataPointer, Length);
                    source[..count].CopyTo(destination);
                }
                finally
                {
                    device.Context.Unmap(Buffer, 0);
                }
                return;
            }

            fixed (T* pointer = source)
            {
                device.Context.UpdateSubresource(
                    Buffer,
                    0,
                    new Box(0, 0, 0, stride * count, 1, 1),
                    (nint)pointer,
                    0,
                    0);
            }
        }

        // 確保は使用量を上回ることがあるため、使用量ぶんのビューで消す。
        public void Clear(int count)
        {
            using var scope = device.Enter();

            device.Context.ClearUnorderedAccessView(
                count >= Length ? Uav : PartialUav(count), new Int4(0, 0, 0, 0));
        }

        ID3D11UnorderedAccessView PartialUav(int count)
        {
            if (partialCount != count)
            {
                disposer.RemoveAndDispose(ref partialUav);
                partialUav = device.Device.CreateUnorderedAccessView(
                    Buffer, new UnorderedAccessViewDescription(Buffer, Format.Unknown, 0, count));
                disposer.Collect(partialUav);
                partialCount = count;
            }

            return partialUav!;
        }

        public void CopyFrom(ComputeBuffer<T> source)
        {
            // CopyResource は同じ大きさを要求するが戻り値が無く、違反しても呼び出し側は気付けない。
            // https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-copyresource
            // ("Must have identical dimensions (including width, height, depth, and size as appropriate)")
            if (source.Length != Length)
                throw new ArgumentException("複写元と複写先の要素数が違います。", nameof(source));

            using var scope = device.Enter();

            device.Context.CopyResource(Buffer, source.Buffer);
        }

        public unsafe void Readback(Span<T> destination)
        {
            var count = Math.Min(destination.Length, Length);
            if (count <= 0)
                return;

            using var scope = device.Enter();

            if (staging is null)
            {
                staging = device.Device.CreateBuffer(new BufferDescription(
                    stride * Length,
                    BindFlags.None,
                    ResourceUsage.Staging,
                    CpuAccessFlags.Read,
                    ResourceOptionFlags.BufferStructured,
                    stride), (SubresourceData?)null);
                disposer.Collect(staging);
            }

            device.Context.CopyResource(staging, Buffer);

            var mapped = device.Context.Map(staging, 0, MapMode.Read, MapFlags.None);
            try
            {
                var source = new ReadOnlySpan<T>((void*)mapped.DataPointer, Length);
                source[..count].CopyTo(destination);
            }
            finally
            {
                device.Context.Unmap(staging, 0);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposer.DisposeAndClear();
            disposed = true;
        }
    }

    internal sealed class ComputeConstantBuffer : IDisposable
    {
        readonly ComputeShaderDevice device;
        readonly int byteSize;
        bool disposed;

        public ID3D11Buffer Buffer { get; }

        public ComputeConstantBuffer(ComputeShaderDevice device, int byteCount)
        {
            this.device = device;
            byteSize = (byteCount + 15) / 16 * 16;
            Buffer = device.Device.CreateBuffer(new BufferDescription(
                byteSize,
                BindFlags.ConstantBuffer,
                ResourceUsage.Dynamic,
                CpuAccessFlags.Write,
                ResourceOptionFlags.None,
                0), (SubresourceData?)null);
        }

        public unsafe void Update<TValue>(in TValue value) where TValue : unmanaged
        {
            using var scope = device.Enter();

            var mapped = device.Context.Map(Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
            try
            {
                var destination = new Span<byte>((void*)mapped.DataPointer, byteSize);
                destination.Clear();
                MemoryMarshal.Write(destination, in value);
            }
            finally
            {
                device.Context.Unmap(Buffer, 0);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            Buffer.Dispose();
            disposed = true;
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;
using MapFlags = Vortice.Direct3D11.MapFlags;

namespace YukkuriMovieMaker.Plugin.Community.Commons.Compute
{
    internal sealed class ComputeBuffer<T> : IDisposable where T : unmanaged
    {
        readonly DisposeCollector disposer = new();
        readonly ComputeShaderDevice device;
        readonly int stride = Unsafe.SizeOf<T>();
        ID3D11Buffer? staging;
        readonly ID3D11UnorderedAccessView? uav;
        readonly bool writable;
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

            Srv = device.Device.CreateShaderResourceView(Buffer, new ShaderResourceViewDescription(Buffer, Format.Unknown, 0, length));
            disposer.Collect(Srv);

            if (writable)
            {
                uav = device.Device.CreateUnorderedAccessView(Buffer, new UnorderedAccessViewDescription(Buffer, Format.Unknown, 0, length));
                disposer.Collect(uav);
            }
        }

        // 書き込み不可の領域は Dynamic なので破棄写経で、書き込み可能な領域は Default なので
        // 部分更新で送る。Default に対する WriteDiscard は D3D11 が受け付けない。
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

        public void Clear()
        {
            using var scope = device.Enter();

            device.Context.ClearUnorderedAccessView(Uav, new Int4(0, 0, 0, 0));
        }

        public void CopyFrom(ComputeBuffer<T> source)
        {
            // D3D11 は大きさの違う複写を黙って捨てるため、ここで気付けるようにする。
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

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering.Buffers;

internal sealed class ConstantBuffer<T> : IDisposable where T : unmanaged
{
    private const int Alignment = 16;

    private ID3D11Buffer? _buffer;

    public ConstantBuffer(ID3D11Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        int alignedSize = (Marshal.SizeOf<T>() + Alignment - 1) & ~(Alignment - 1);
        _buffer = device.CreateBuffer(new BufferDescription(
            alignedSize,
            BindFlags.ConstantBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write));
    }

    public ID3D11Buffer Buffer => _buffer ?? throw new ObjectDisposedException(nameof(ConstantBuffer<T>));

    public unsafe void Update(ID3D11DeviceContext context, ref T data)
    {
        if (_buffer is null) return;

        context.Map(_buffer, 0, MapMode.WriteDiscard, MapFlags.None, out var mapped);
        Unsafe.Copy(mapped.DataPointer.ToPointer(), ref data);
        context.Unmap(_buffer, 0);
    }

    public void Dispose()
    {
        _buffer?.Dispose();
        _buffer = null;
    }
}

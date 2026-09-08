using System;
using System.Collections.Generic;
using System.Threading;
using Vortice.DXGI;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Commons.Compute
{
    internal sealed class ComputeShaderDevice : IDisposable
    {
        const int MaxResources = 3;
        const int MaxTargets = 2;

        // バイト列だけ共有し、実体は個体ごとに持って寿命を分ける。
        static readonly Lock bytecodeLock = new();
        static readonly Dictionary<string, byte[]> bytecodes = [];

        readonly DisposeCollector disposer = new();
        readonly Dictionary<string, ID3D11ComputeShader> shaders = [];
        readonly ID3D11ShaderResourceView?[] resourceSlots = new ID3D11ShaderResourceView?[MaxResources];
        readonly ID3D11UnorderedAccessView?[] targetSlots = new ID3D11UnorderedAccessView?[MaxTargets];
        readonly ID3D11Multithread multithread;
        bool disposed;

        public ID3D11Device Device { get; }
        public ID3D11DeviceContext Context { get; }
        public bool IsSupported { get; }

        // UAV に使えるかは形式とデバイスごとに決まるため、構築時に一度だけ調べる。
        // https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_format_support
        // ("Which resources are supported for a given format and given device")
        public bool SupportsWritableSurface { get; }

        public ComputeShaderDevice(IGraphicsDevicesAndContext devices)
        {
            Device = devices.D3D.Device;
            Context = devices.D3D.DeviceContext;
            multithread = devices.D3D.Multithread;
            // cs_5_0（シェーダーモデル 5.0）は機能レベル 11_0 から。
            // https://learn.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-devices-downlevel-intro
            IsSupported = Device.FeatureLevel >= FeatureLevel.Level_11_0;
            SupportsWritableSurface = Device.CheckFormatSupport(Format.B8G8R8A8_UNorm)
                .HasFlag(FormatSupport.TypedUnorderedAccessView);
        }

        // ComputeGroup.hlsli の GROUP_X / GROUP_Y / LINEAR_GROUP_THREADS に一致させる。
        public const int PixelGroupSize = 8;
        public const int LinearGroupSize = 64;

        public static int PixelGroups(int extent) => GroupCount(extent, PixelGroupSize);

        public static int LinearGroups(int count) => GroupCount(count, LinearGroupSize);

        static int GroupCount(int extent, int threads) => (extent + threads - 1) / threads;

        public Scope Enter() => new(multithread);

        public void Dispatch(
            string shaderName,
            ID3D11Buffer constants,
            int groupsX,
            int groupsY,
            ID3D11ShaderResourceView resource0,
            ID3D11UnorderedAccessView target0,
            ID3D11UnorderedAccessView? target1 = null)
            => Run(shaderName, constants, groupsX, groupsY, resource0, null, null, target0, target1);

        public void Dispatch(
            string shaderName,
            ID3D11Buffer constants,
            int groupsX,
            int groupsY,
            ID3D11ShaderResourceView resource0,
            ID3D11ShaderResourceView resource1,
            ID3D11UnorderedAccessView target0,
            ID3D11UnorderedAccessView? target1 = null)
            => Run(shaderName, constants, groupsX, groupsY, resource0, resource1, null, target0, target1);

        public void Dispatch(
            string shaderName,
            ID3D11Buffer constants,
            int groupsX,
            int groupsY,
            ID3D11ShaderResourceView resource0,
            ID3D11ShaderResourceView resource1,
            ID3D11ShaderResourceView resource2,
            ID3D11UnorderedAccessView target0,
            ID3D11UnorderedAccessView? target1 = null)
            => Run(shaderName, constants, groupsX, groupsY, resource0, resource1, resource2, target0, target1);

        void Run(
            string shaderName,
            ID3D11Buffer constants,
            int groupsX,
            int groupsY,
            ID3D11ShaderResourceView resource0,
            ID3D11ShaderResourceView? resource1,
            ID3D11ShaderResourceView? resource2,
            ID3D11UnorderedAccessView target0,
            ID3D11UnorderedAccessView? target1)
        {
            var resourceCount = resource2 is not null ? 3 : resource1 is not null ? 2 : 1;
            var targetCount = target1 is not null ? 2 : 1;

            // 控えの配列は個体で共有するため、書き換えは錠の内側だけで行う。
            using var scope = Enter();

            resourceSlots[0] = resource0;
            resourceSlots[1] = resource1;
            resourceSlots[2] = resource2;
            targetSlots[0] = target0;
            targetSlots[1] = target1;

            Context.CSSetShader(GetShader(shaderName));
            Context.CSSetConstantBuffer(0, constants);
            Context.CSSetShaderResources(0, resourceCount, resourceSlots!);
            Context.CSSetUnorderedAccessViews(0, targetCount, targetSlots!);

            Context.Dispatch(groupsX, groupsY, 1);

            // 同じ面を入力と出力へ同時に束縛するとランタイムが入力を外す。D2D へ渡す前に解く。
            // https://learn.microsoft.com/en-us/windows/win32/direct3d11/hazard-tracking-versus-tile-pool-resources
            // ("If such a case is encountered, the runtime unbinds the input")
            Array.Clear(resourceSlots);
            Array.Clear(targetSlots);
            Context.CSSetShaderResources(0, resourceCount, resourceSlots!);
            Context.CSSetUnorderedAccessViews(0, targetCount, targetSlots!);
            Context.CSSetShader(null);
        }

        ID3D11ComputeShader GetShader(string name)
        {
            if (shaders.TryGetValue(name, out var cached))
                return cached;

            var shader = Device.CreateComputeShader(GetBytecode(name), null);
            disposer.Collect(shader);
            shaders[name] = shader;
            return shader;
        }

        static byte[] GetBytecode(string name)
        {
            lock (bytecodeLock)
            {
                if (bytecodes.TryGetValue(name, out var cached))
                    return cached;

                var bytes = PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(name));
                bytecodes[name] = bytes;
                return bytes;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            shaders.Clear();
            disposer.DisposeAndClear();
            disposed = true;
        }

        internal readonly struct Scope : IDisposable
        {
            readonly ID3D11Multithread multithread;

            public Scope(ID3D11Multithread multithread)
            {
                this.multithread = multithread;
                multithread.Enter();
            }

            public void Dispose() => multithread.Leave();
        }
    }
}

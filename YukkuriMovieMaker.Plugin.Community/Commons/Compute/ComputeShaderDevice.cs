using System;
using System.Collections.Generic;
using System.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Commons.Compute
{
    internal sealed class ComputeShaderDevice : IDisposable
    {
        const int MaxResources = 2;
        const int MaxTargets = 4;

        // シェーダーのバイト列はプロセス内で共有する。実体はエフェクトの個体ごとに持つため、
        // 個体を破棄しても他へ影響せず、パックリソースの読み出しは名前ごとに一度で済む。
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

        public ComputeShaderDevice(IGraphicsDevicesAndContext devices)
        {
            Device = devices.D3D.Device;
            Context = devices.D3D.DeviceContext;
            multithread = devices.D3D.Multithread;
            IsSupported = Device.FeatureLevel >= FeatureLevel.Level_11_0;
        }

        public static int GroupCount(int extent, int threads) => (extent + threads - 1) / threads;

        public Scope Enter() => new(multithread);

        public void Dispatch(
            string shaderName,
            ID3D11Buffer constants,
            int groupsX,
            int groupsY,
            ID3D11UnorderedAccessView target0,
            ID3D11UnorderedAccessView? target1 = null,
            ID3D11UnorderedAccessView? target2 = null,
            ID3D11UnorderedAccessView? target3 = null)
            => Run(shaderName, constants, groupsX, groupsY, 0, target0, target1, target2, target3);

        public void Dispatch(
            string shaderName,
            ID3D11Buffer constants,
            int groupsX,
            int groupsY,
            ID3D11ShaderResourceView resource0,
            ID3D11UnorderedAccessView target0,
            ID3D11UnorderedAccessView? target1 = null,
            ID3D11UnorderedAccessView? target2 = null,
            ID3D11UnorderedAccessView? target3 = null)
        {
            resourceSlots[0] = resource0;
            Run(shaderName, constants, groupsX, groupsY, 1, target0, target1, target2, target3);
        }

        public void Dispatch(
            string shaderName,
            ID3D11Buffer constants,
            int groupsX,
            int groupsY,
            ID3D11ShaderResourceView resource0,
            ID3D11ShaderResourceView resource1,
            ID3D11UnorderedAccessView target0,
            ID3D11UnorderedAccessView? target1 = null,
            ID3D11UnorderedAccessView? target2 = null,
            ID3D11UnorderedAccessView? target3 = null)
        {
            resourceSlots[0] = resource0;
            resourceSlots[1] = resource1;
            Run(shaderName, constants, groupsX, groupsY, 2, target0, target1, target2, target3);
        }

        void Run(
            string shaderName,
            ID3D11Buffer constants,
            int groupsX,
            int groupsY,
            int resourceCount,
            ID3D11UnorderedAccessView target0,
            ID3D11UnorderedAccessView? target1,
            ID3D11UnorderedAccessView? target2,
            ID3D11UnorderedAccessView? target3)
        {
            targetSlots[0] = target0;
            targetSlots[1] = target1;
            targetSlots[2] = target2;
            targetSlots[3] = target3;

            var targetCount = target3 is not null ? 4 : target2 is not null ? 3 : target1 is not null ? 2 : 1;

            using var scope = Enter();

            Context.CSSetShader(GetShader(shaderName));
            Context.CSSetConstantBuffer(0, constants);
            if (resourceCount > 0)
                Context.CSSetShaderResources(0, resourceCount, resourceSlots!);
            Context.CSSetUnorderedAccessViews(0, targetCount, targetSlots!);

            Context.Dispatch(groupsX, groupsY, 1);

            // 同じテクスチャを D2D が読み書きするため、束縛は必ずここで解く。
            Array.Clear(resourceSlots);
            Array.Clear(targetSlots);
            if (resourceCount > 0)
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
            using (bytecodeLock.EnterScope())
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

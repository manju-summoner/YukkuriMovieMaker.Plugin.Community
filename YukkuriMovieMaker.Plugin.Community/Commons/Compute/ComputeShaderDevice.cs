using System;
using System.Collections.Generic;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Commons.Compute
{
    internal sealed class ComputeShaderDevice : IDisposable
    {
        readonly DisposeCollector disposer = new();
        readonly Dictionary<string, ID3D11ComputeShader> shaders = [];
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

        public ID3D11ComputeShader GetShader(string name)
        {
            if (shaders.TryGetValue(name, out var cached))
                return cached;

            var bytes = PackResourceReader.ReadAllBytes(ShaderResourceUri.Get(name));
            var shader = Device.CreateComputeShader(bytes, null);
            disposer.Collect(shader);
            shaders[name] = shader;
            return shader;
        }

        public Scope Enter() => new(multithread);

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

using System.Windows.Media.Imaging;
using Vortice.Direct3D11;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Textures;

internal interface ITextureService : IDisposable
{
    BitmapSource Load(string path);
    (ID3D11ShaderResourceView? Srv, long GpuBytes, bool HasTransparency) CreateShaderResourceView(string path, ID3D11Device device);
    void EvictGpuTexture(string path, ID3D11Device device);
}

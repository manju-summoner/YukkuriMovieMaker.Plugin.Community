using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

internal static class GradientMapServices
{
    internal static readonly IServiceRegistry Container = BuildContainer();

    private static IServiceRegistry BuildContainer()
    {
        var registry = new ServiceRegistry();
        registry.RegisterSingleton<IGradientTextureFactory>(new GradientTextureFactory());
        registry.RegisterSingleton<IGrdManifestReader>(new GrdManifestReader());
        registry.RegisterFactory<IResourceRegistry>(() => new ResourceRegistry());
        return registry;
    }
}

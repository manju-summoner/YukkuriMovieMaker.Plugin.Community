using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Commons
{
    internal class ShaderResourceUri
    {
        public static Uri Get(string shaderName) => new($"pack://application:,,,/YukkuriMovieMaker.Plugin.Community;component/Resources/Shader/{shaderName}.cso");
    }
}

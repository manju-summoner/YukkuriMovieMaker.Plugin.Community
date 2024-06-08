using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Rainbow
{
    internal class RainbowStopsGenerator
    {
        public static GradientStop[] Create(double hueOffset, double saturate, double value, RainbowColorSpace colorSpace)
        {
            var delta = 30;
            var count = 360 / delta;
            var stops = new GradientStop[count + 1];
            for (int i = 0; i <= count; i++)
            {
                
                var h = (hueOffset + (double)i / count) % 1;
                var s = saturate;
                var v = value;
                Color4 color;
                if(colorSpace is RainbowColorSpace.HSV)
                {
                    color = ColorEx.FromHSV(h, s, v, 1d).ToColor4();
                }
                else
                {
                    color = ColorEx.FromLch(v, s, h, 1d).ToColor4();
                }
                stops[i] = new GradientStop
                {
                    Position = (float)i / count,
                    Color = color,
                };
                
            }
            return stops;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Ink;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal record SerializableStroke(SerializableStylusPoint[] StylusPoints, DrawingAttributes DrawingAttributes)
    {
        public SerializableStroke() : this([], new DrawingAttributes())
        {

        }
        public SerializableStroke(Stroke stroke) : 
            this(
                stroke.StylusPoints.Select(x=>new SerializableStylusPoint(x)).ToArray(), stroke.DrawingAttributes)
        {

        }

        public Stroke ToStroke()
        {
            return new Stroke(
                new(StylusPoints.Select(x=>x.ToStylusPoint())),
                DrawingAttributes);
        }

        public bool DeeqEqueals(SerializableStroke other)
        {
            return
                StylusPoints.Length == other.StylusPoints.Length
                && StylusPoints.Zip(other.StylusPoints).All(pair => pair.First == pair.Second)
                //DrawingAttributes.EqualsはDeepEquals
                //https://github.com/dotnet/wpf/blob/27ffd5aa31a1aec85f03ec137ca384f61b5d6ab8/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Ink/DrawingAttributes.cs#L524
                && DrawingAttributes.Equals(other.DrawingAttributes);
        }
    }

}

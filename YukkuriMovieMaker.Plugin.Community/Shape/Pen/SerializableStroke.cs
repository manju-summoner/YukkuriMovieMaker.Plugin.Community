using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal record SerializableStroke(SerializableStylusPoint[] StylusPoints, DrawingAttributes DrawingAttributes)
    {
        public SerializableStroke() : this([], new DrawingAttributes())
        {

        }
        public SerializableStroke(Stroke stroke) : this(ToPoints(stroke.StylusPoints), stroke.DrawingAttributes)
        {

        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int[]? FillFigures { get; init; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsPencil { get; init; }

        public static SerializableStylusPoint[] ToPoints(StylusPointCollection stylusPoints)
        {
            var points = new SerializableStylusPoint[stylusPoints.Count];
            for (var i = 0; i < points.Length; i++)
                points[i] = new SerializableStylusPoint(stylusPoints[i]);
            return points;
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
                && DrawingAttributes.Equals(other.DrawingAttributes)
                && IsSameFigures(FillFigures, other.FillFigures)
                && IsPencil == other.IsPencil;
        }

        static bool IsSameFigures(int[]? figures, int[]? other)
        {
            if (figures is null || other is null)
                return figures is null && other is null;
            if (figures.Length != other.Length)
                return false;
            for (var i = 0; i < figures.Length; i++)
            {
                if (figures[i] != other[i])
                    return false;
            }
            return true;
        }
    }

}

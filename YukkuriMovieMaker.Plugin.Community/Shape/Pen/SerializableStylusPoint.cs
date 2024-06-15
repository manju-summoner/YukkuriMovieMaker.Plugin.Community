using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal record SerializableStylusPoint(double X, double Y, float PressureFactor)
    {
        public SerializableStylusPoint() : this(0, 0, 0)
        {

        }
        public SerializableStylusPoint(StylusPoint stylusPoint):this(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor)
        {

        }

        public StylusPoint ToStylusPoint()
        {
            return new StylusPoint(X, Y, PressureFactor);
        }
    }

}

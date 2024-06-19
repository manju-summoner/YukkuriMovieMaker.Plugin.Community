using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular.DistantLight
{
    internal class DistantLightEffectProcessor(IGraphicsDevicesAndContext devices, DistantLightEffect distantLightEffect) : DiffuseAndSpecularEffectProcessorBase<DistantDiffuseWrapper, DistantSpecularWrapper>(devices, distantLightEffect)
    {
        double azimuth,elevation;
        protected override DistantDiffuseWrapper CreateDiffuseEffect(IGraphicsDevicesAndContext devices)
        {
            return new DistantDiffuseWrapper(new DistantDiffuse(devices.DeviceContext) { ScaleMode = Vortice.Direct2D1.DistantDiffuseScaleMode.HighQualityCubic });
        }

        protected override DistantSpecularWrapper CreateSpecularEffect(IGraphicsDevicesAndContext devices)
        {
            return new DistantSpecularWrapper(new DistantSpecular(devices.DeviceContext) { ScaleMode = Vortice.Direct2D1.DistantSpecularScaleMode.HighQualityCubic });
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if(specular is null || diffuse is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var azimuth = distantLightEffect.Azimuth.GetValue(frame, length, fps);
            var elevation = distantLightEffect.Elevation.GetValue(frame, length, fps);
            azimuth = (azimuth % 360 + 360) % 360;
            elevation = (elevation % 360 + 360) % 360;

            if (isFirst || this.azimuth != azimuth || this.elevation != elevation)
            {
                specular.Azimuth = (float)azimuth;
                specular.Elevation = (float)elevation;
                diffuse.Azimuth = (float)azimuth;
                diffuse.Elevation = (float)elevation;
            }

            var controllerPos = 
                Vector2.Transform(
                    new(100, 0), 
                    Matrix3x2.CreateRotation(MathF.PI * (float)azimuth / 180));

            var controller = new VideoEffectController(
                distantLightEffect,
                [
                    new ControllerPoint(
                        new (controllerPos.X, controllerPos.Y, 0))
                    {
                        Shape = VideoControllerPointShape.None 
                    },
                    new ControllerPoint(new(0,0,0))
                    { 
                        Shape = VideoControllerPointShape.None 
                    },
                ])
            { Connection = VideoControllerPointConnection.Line };

            //base.Update(effectDescription)でisFirstをfalseにする
            this.azimuth = azimuth;
            this.elevation = elevation;

            return base.Update(effectDescription) with
            {
                Controllers =
                [
                    ..effectDescription.DrawDescription.Controllers,
                    controller
                ],
            };
        }
    }
}

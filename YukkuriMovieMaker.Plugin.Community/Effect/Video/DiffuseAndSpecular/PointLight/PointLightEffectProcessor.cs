using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular.PointLight
{
    internal class PointLightEffectProcessor(IGraphicsDevicesAndContext devices, PointLightEffect pointLightEffect) : DiffuseAndSpecularEffectProcessorBase<PointDiffuseWrapper, PointSpecularWrapper>(devices, pointLightEffect)
    {
        double x, y, z;
        protected override PointDiffuseWrapper CreateDiffuseEffect(IGraphicsDevicesAndContext devices)
        {
            return new PointDiffuseWrapper(new PointDiffuse(devices.DeviceContext) { ScaleMode = Vortice.Direct2D1.PointDiffuseScaleMode.HighQualityCubic });
        }

        protected override PointSpecularWrapper CreateSpecularEffect(IGraphicsDevicesAndContext devices)
        {
            return new PointSpecularWrapper(new PointSpecular(devices.DeviceContext) { ScaleMode = Vortice.Direct2D1.PointSpecularScaleMode.HighQualityCubic });
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if(specular is null || diffuse is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var x = pointLightEffect.X.GetValue(frame, length, fps);
            var y = pointLightEffect.Y.GetValue(frame, length, fps);
            var z = pointLightEffect.Z.GetValue(frame, length, fps);

            if (isFirst || this.x != x || this.y != y || this.z != z)
            {
                var light = new Vector3((float)x, (float)y, (float)z);
                specular.LightPosition = light;
                diffuse.LightPosition = light;
            }

            var controller = new VideoEffectController(
                pointLightEffect,
                [
                    new ControllerPoint(
                        new ((float)x, (float)y, (float)z),
                        arg=>
                        {
                            pointLightEffect.X.AddToEachValues(arg.Delta.X);
                            pointLightEffect.Y.AddToEachValues(arg.Delta.Y);
                        })
                    {
                        OnMouseWheel = arg =>
                        {
                            pointLightEffect.Z.AddToEachValues(arg.Delta);
                        }
                    }
                ]);

            //base.Update(effectDescription)でisFirstをfalseにする
            this.x = x;
            this.y = y;
            this.z = z;

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

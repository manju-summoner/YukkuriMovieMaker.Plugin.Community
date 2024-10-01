using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorBlindness
{
    internal class ColorBlindnessEffectProcessor(IGraphicsDevicesAndContext devices, ColorBlindnessEffect item) : VideoEffectProcessorBase(devices)
    {
        ColorBlindnessCustomEffect? effect;

        bool isFirst = true;
        double strength;
        ColorBlindnessType type;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var type = item.Type;
            var strength = item.Strength.GetValue(frame, length, fps) / 100;


            if (isFirst || this.type != type)
                effect.Type = (int)type;
            if (isFirst || this.strength != strength)
                effect.Strength = (float)strength;

            isFirst = false;
            this.type = type;
            this.strength = strength;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new ColorBlindnessCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }
            disposer.Collect(effect);

            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    internal record class RateLimits(int Free, int Tier1, int Tier2, int Tier3)
    {
        public int GetRateLimit(int tier)
        {
            return tier switch
            {
                1 => Tier1,
                2 => Tier2,
                3 => Tier3,
                _ => Free
            };
        }
    }
}

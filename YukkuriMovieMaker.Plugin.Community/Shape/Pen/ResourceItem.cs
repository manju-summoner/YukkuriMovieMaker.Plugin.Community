using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{

    class ResourceItem<TKey, TValue>(TKey key, TValue value) : IDisposable where TValue : IDisposable
    {
        public TKey Key { get; } = key;
        public TValue Value { get; } = value;
        public bool IsUsed { get; set; } = true;

        public void Dispose()
        {
            Value.Dispose();
        }
    }
}

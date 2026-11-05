using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Services
{
    public interface IMotionDriverCatalog
    {
        void Add(string name, Func<IMotionDriver> factory);
        IMotionDriver Create(string name);
    }

    public sealed class MotionDriverCatalog : IMotionDriverCatalog
    {
        private readonly Dictionary<string, Func<IMotionDriver>> _m =
            new Dictionary<string, Func<IMotionDriver>>(StringComparer.OrdinalIgnoreCase);

        public void Add(string name, Func<IMotionDriver> factory)
        {
            if (_m.ContainsKey(name)) throw new InvalidOperationException("运动驱动重名：" + name);
            _m[name] = factory;
        }

        public IMotionDriver Create(string name)
        {
            Func<IMotionDriver> f;
            if (!_m.TryGetValue(name ?? "", out f))
                throw new InvalidOperationException("未注册的运动驱动：" + name + "（已注册：" + string.Join("、", _m.Keys) + "）");
            return f();
        }
    }
}

using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Scripting
{
    public sealed class ScriptCommandRegistry : IScriptCommandRegistry
    {
        private readonly Dictionary<string, IScriptCommand> _map =
            new Dictionary<string, IScriptCommand>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();

        public void Register(IScriptCommand c)
        {
            lock (_gate)
            {
                if (_map.ContainsKey(c.Spec.Name)) throw new InvalidOperationException("脚本命令重名：" + c.Spec.Name);
                _map[c.Spec.Name] = c;
            }
        }

        public bool TryGet(string name, out IScriptCommand c) { lock (_gate) return _map.TryGetValue(name ?? "", out c); }
        public IReadOnlyList<IScriptCommand> All { get { lock (_gate) return _map.Values.ToList(); } }
    }
}

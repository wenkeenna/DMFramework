using DM.Core.TaskList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.TaskList
{
    public sealed class TaskTypeRegistry : ITaskTypeRegistry
    {
        private readonly Dictionary<string, ITaskType> _map = new Dictionary<string, ITaskType>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();

        public void Register(ITaskType type)
        {
            lock (_gate)
            {
                if (_map.ContainsKey(type.Key)) throw new InvalidOperationException("任务类型重名：" + type.Key);
                _map[type.Key] = type;
            }
        }

        public bool TryGet(string key, out ITaskType type) { lock (_gate) return _map.TryGetValue(key ?? "", out type); }

        public IReadOnlyList<ITaskType> All
        { get { lock (_gate) return _map.Values.OrderBy(t => t.Group).ThenBy(t => t.DisplayName).ToList(); } }
    }
}

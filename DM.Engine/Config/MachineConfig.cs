using DM.Core.Common;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.Config
{
    public sealed class MachineConfig : IMachineConfig
    {
        private readonly JObject _root;

        public MachineConfig(AppPaths paths)
        {
            string file = Path.Combine(paths.Config, "machine.json");
            if (!File.Exists(file)) { _root = new JObject(); return; }
            try { _root = JObject.Parse(File.ReadAllText(file)); }
            catch (Exception ex)
            { throw new InvalidOperationException("machine.json 格式错误（" + file + "）：" + ex.Message, ex); }
        }

        public T Get<T>(string section) where T : class, new()
        {
            JToken t = _root[section];
            return (t == null ? null : t.ToObject<T>()) ?? new T();
        }
    }
}

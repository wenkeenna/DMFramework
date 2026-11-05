using DM.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Engine.Data
{
    /// <summary>键不区分大小写；值没变化不触发 Changed；文件是扁平 JSON {"Root.Child": 值}，写入先写临时文件再替换。</summary>
    public sealed class DataContainer : IDataContainer
    {
        private sealed class Entry { public object Value; public DataScope Scope; }

        private readonly Dictionary<string, Entry> _d = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly object _g = new object();
        private int _loading;

        public event EventHandler<DataChangedEventArgs> Changed;
        public event EventHandler Loaded;

        public bool TryGet<T>(string key, out T value)
        {
            value = default(T);
            object o;
            lock (_g) { Entry e; if (!_d.TryGetValue(key ?? "", out e)) return false; o = e.Value; }
            if (o == null) return false;
            if (o is T) { value = (T)o; return true; }
            try { value = (T)Convert.ChangeType(o, typeof(T), CultureInfo.InvariantCulture); return true; }
            catch { return false; }
        }

        public T Get<T>(string key, T fallback = default(T)) { T v; return TryGet(key, out v) ? v : fallback; }
        public bool Contains(string key) { lock (_g) return _d.ContainsKey(key ?? ""); }

        public void Set(string key, object value, DataScope scope = DataScope.Project)
        {
            string r, c;
            if (!DataKey.TrySplit(key, out r, out c)) throw new ArgumentException("键格式应为 Root.Child：" + key);
            object old;
            lock (_g)
            {
                Entry e;
                bool existed = _d.TryGetValue(key, out e);
                old = existed ? e.Value : null;
                if (existed && e.Scope == scope && Equals(old, value)) return;
                _d[key] = new Entry { Value = value, Scope = scope };
            }
            Raise(new DataChangedEventArgs { Key = key, Scope = scope, OldValue = old, NewValue = value });
        }

        public bool Remove(string key)
        {
            Entry e;
            lock (_g)
            {
                if (!_d.TryGetValue(key ?? "", out e)) return false;
                _d.Remove(key);
            }
            Raise(new DataChangedEventArgs { Key = key, Scope = e.Scope, OldValue = e.Value });
            return true;
        }

        public IReadOnlyList<KeyValuePair<string, object>> Snapshot(DataScope scope)
        {
            lock (_g)
                return _d.Where(x => x.Value.Scope == scope)
                         .Select(x => new KeyValuePair<string, object>(x.Key, x.Value.Value)).ToList();
        }

        public IDisposable BeginLoad() { Interlocked.Increment(ref _loading); return new LoadScope(this); }

        private void Raise(DataChangedEventArgs e)
        {
            if (Volatile.Read(ref _loading) > 0) return;
            var h = Changed; if (h != null) h(this, e);
        }

        private sealed class LoadScope : IDisposable
        {
            private DataContainer _c;
            public LoadScope(DataContainer c) { _c = c; }
            public void Dispose()
            {
                var c = Interlocked.Exchange(ref _c, null);
                if (c == null) return;
                if (Interlocked.Decrement(ref c._loading) == 0) { var h = c.Loaded; if (h != null) h(c, EventArgs.Empty); }
            }
        }

        // ───── 文件读写（引擎内部使用，不在接口上）─────
        /// <summary>用文件内容替换指定作用域的全部数据；文件不存在视为空；格式错误抛异常。</summary>
        public void LoadFile(string path, DataScope scope)
        {
            var items = new List<KeyValuePair<string, object>>();
            if (File.Exists(path))
            {
                JObject o = JObject.Parse(File.ReadAllText(path));
                foreach (JProperty p in o.Properties())
                    items.Add(new KeyValuePair<string, object>(p.Name,
                        p.Value is JValue ? ((JValue)p.Value).Value : p.Value.ToString(Formatting.None)));
            }
            using (BeginLoad())
            {
                ClearScopeCore(scope);
                foreach (var kv in items) Set(kv.Key, kv.Value, scope);
            }
        }

        public void SaveFile(string path, DataScope scope)
        {
            var o = new JObject();
            foreach (var kv in Snapshot(scope).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                o[kv.Key] = JToken.FromObject(kv.Value ?? "");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, o.ToString(Formatting.Indented));
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }

        public void ClearScope(DataScope scope) { using (BeginLoad()) ClearScopeCore(scope); }

        private void ClearScopeCore(DataScope scope)
        {
            lock (_g)
                foreach (string k in _d.Where(x => x.Value.Scope == scope).Select(x => x.Key).ToList()) _d.Remove(k);
        }
    }
}

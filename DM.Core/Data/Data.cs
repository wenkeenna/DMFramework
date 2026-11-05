using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Core.Data
{
    /// <summary>Runtime：不落盘不审计；Project：随项目；Machine：属于这台设备（标定值等）。</summary>
    public enum DataScope { Runtime = 0, Project = 1, Machine = 2 }

    public sealed class DataChangedEventArgs : EventArgs
    {
        public string Key { get; set; }             // "Root.Child"
        public DataScope Scope { get; set; }
        public object OldValue { get; set; }        // 新增时为 null
        public object NewValue { get; set; }        // 删除时为 null
    }

    /// <summary>项目数据的唯一来源。键统一为 "Root.Child"，脚本和任务里都用同一种写法。</summary>
    public interface IDataContainer
    {
        bool TryGet<T>(string key, out T value);
        T Get<T>(string key, T fallback = default(T));
        bool Contains(string key);
        void Set(string key, object value, DataScope scope = DataScope.Project);
        bool Remove(string key);
        IReadOnlyList<KeyValuePair<string, object>> Snapshot(DataScope scope);

        event EventHandler<DataChangedEventArgs> Changed;

        /// <summary>批量加载期间不逐条触发 Changed，结束时触发一次 Loaded。</summary>
        IDisposable BeginLoad();
        event EventHandler Loaded;
    }

    public static class DataKey
    {
        /// <summary>"Root.Child" → (Root, Child)，两段都必须非空。</summary>
        public static bool TrySplit(string key, out string root, out string child)
        {
            root = child = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            int i = key.IndexOf('.');
            if (i <= 0 || i >= key.Length - 1) return false;
            root = key.Substring(0, i).Trim();
            child = key.Substring(i + 1).Trim();
            return root.Length > 0 && child.Length > 0;
        }
    }
}

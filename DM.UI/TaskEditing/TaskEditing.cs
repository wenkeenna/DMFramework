using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.UI.TaskEditing
{
    /// <summary>
    /// 递归监听一个数据对象图：属性变化、集合增删，任何变化都调用 onChanged；新加入集合的元素自动纳入监听。
    /// 数据类继承 BindableBase、集合用 ObservableCollection，编辑器就不需要手写任何监听。
    /// 限制：整体替换集合属性（赋新集合）不会被跟踪，数据类里集合属性一律只读（只有 getter）。
    /// </summary>
    public sealed class ChangeTracker : IDisposable
    {
        private readonly Action _changed;
        private readonly HashSet<object> _hooked = new HashSet<object>();

        public ChangeTracker(object root, Action changed) { _changed = changed; Track(root); }

        private void Track(object o)
        {
            if (o == null || _hooked.Contains(o)) return;
            var npc = o as INotifyPropertyChanged;
            var ncc = o as INotifyCollectionChanged;
            if (npc == null && ncc == null) return;

            _hooked.Add(o);
            if (npc != null)
            {
                npc.PropertyChanged += OnProp;
                foreach (var p in o.GetType().GetProperties())
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0 || p.PropertyType.IsValueType || p.PropertyType == typeof(string)) continue;
                    try { Track(p.GetValue(o, null)); } catch (Exception) { }
                }
            }
            if (ncc != null)
            {
                ncc.CollectionChanged += OnColl;
                foreach (object i in (IEnumerable)o) Track(i);
            }
        }

        private void OnProp(object s, PropertyChangedEventArgs e) { _changed(); }

        private void OnColl(object s, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (object i in e.NewItems) Track(i);
            _changed();
        }

        public void Dispose()
        {
            foreach (object o in _hooked)
            {
                var npc = o as INotifyPropertyChanged; if (npc != null) npc.PropertyChanged -= OnProp;
                var ncc = o as INotifyCollectionChanged; if (ncc != null) ncc.CollectionChanged -= OnColl;
            }
            _hooked.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DM.UI
{
    public static class UiThread
    {
        private static Dispatcher D { get { return Application.Current == null ? null : Application.Current.Dispatcher; } }

        /// <summary>在 UI 线程同步执行。协调器在线程池里调用参与者，改界面对象前必须用它。</summary>
        public static void Invoke(Action a)
        { Dispatcher d = D; if (d == null || d.CheckAccess()) a(); else d.Invoke(a); }

        /// <summary>异步投递到 UI 线程。用于在属性 setter 里"拒绝修改并让界面回显旧值"。</summary>
        public static void Post(Action a)
        { Dispatcher d = D; if (d == null) a(); else d.BeginInvoke(a); }
    }
}

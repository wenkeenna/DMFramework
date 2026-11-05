using DM.Core.Audit;
using DM.Core.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Audit.Services
{
    /// <summary>
    /// 订阅数据容器的变化，记录参数修改（旧值 → 新值），数据容器本身不依赖审计。
    /// 过滤规则：Runtime 作用域不记（任务运行时高频写入）；批量加载期间容器不触发 Changed，所以打开项目不会刷屏；
    /// 值没变化容器不触发，所以界面"全部保存"不产生噪音。
    /// </summary>
    public sealed class DataAuditBridge
    {
        private readonly IAuditService _audit;

        public DataAuditBridge(IDataContainer data, IAuditService audit)
        {
            _audit = audit;
            data.Changed += OnChanged;
        }

        private void OnChanged(object sender, DataChangedEventArgs e)
        {
            if (e.Scope == DataScope.Runtime) return;
            string action = e.OldValue == null ? "Add" : e.NewValue == null ? "Remove" : "Edit";
            _audit.Record(AuditCategory.Parameter, action, e.Key, Fmt(e.OldValue), Fmt(e.NewValue), detail: "Scope=" + e.Scope);
        }

        private static string Fmt(object o)
        {
            if (o == null) return null;
            string s = Convert.ToString(o, CultureInfo.InvariantCulture) ?? "";
            return s.Length > 200 ? s.Substring(0, 200) + "…" : s;       // 防止大对象撑爆审计库
        }
    }
}

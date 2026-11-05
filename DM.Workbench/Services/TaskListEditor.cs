using DM.Core.Audit;
using DM.Core.Identity;
using DM.Core.TaskList;
using DM.UI;
using DM.Workbench.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Workbench.Services
{
    /// <summary>
    /// 任务列表的唯一修改入口：增删移、复制、重命名、改参数、转换。
    /// 每个修改都做三件事：权限/运行锁检查 → 修改 → 写审计并标脏。
    /// 只能在 UI 线程使用（Rows 绑定界面）；项目参与者通过 UiThread 切线程后调用。
    /// 编辑需要 Engineer 及以上角色，运行中整体锁定。
    /// </summary>
    public sealed class TaskListEditor
    {
        public static readonly AccessRole EditRole = AccessRole.Engineer;
        private const int MaxAuditText = 4000;

        private readonly ITaskTypeRegistry _types;
        private readonly IAuditService _audit;
        private readonly ICurrentUser _user;
        private bool _locked;

        public TaskListEditor(ITaskTypeRegistry types, IAuditService audit, ICurrentUser user, IIdentityService identity)
        {
            _types = types; _audit = audit; _user = user;
            Rows.CollectionChanged += (s, e) => { for (int i = 0; i < Rows.Count; i++) Rows[i].Order = i + 1; };
            // 登录可能发生在线程池线程（登录窗口把哈希计算放到了后台），事件统一切回 UI 线程
            identity.SessionChanged += (s, e) => UiThread.Invoke(() => ReadOnlyChanged?.Invoke(this, EventArgs.Empty));
        }

        public ObservableCollection<TaskRow> Rows { get; } = new ObservableCollection<TaskRow>();
        public bool IsDirty { get; private set; }

        public bool IsLocked
        {
            get { return _locked; }
            set { if (_locked == value) return; _locked = value; ReadOnlyChanged?.Invoke(this, EventArgs.Empty); }
        }
        public bool IsReadOnly => _locked || _user.Role < EditRole;

        public event EventHandler ReadOnlyChanged;
        public event EventHandler DirtyChanged;
        public event Action<string> Notice;                                  // 编辑界面（会话）和载入修复上报的提示

        public void Report(string message) { Notice?.Invoke(message); }

        // ───── 载入 / 导出 ─────
        public void Load(IEnumerable<TaskItem> items)
        {
            Rows.Clear();
            bool repaired = false;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (TaskItem it in (items ?? Enumerable.Empty<TaskItem>()).Where(x => x != null))
            {
                if (string.IsNullOrWhiteSpace(it.Id) || !ids.Add(it.Id)) { it.Id = NewId(); ids.Add(it.Id); repaired = true; }
                string name = UniqueName(string.IsNullOrWhiteSpace(it.Name) ? "任务" : it.Name, null);
                if (name != it.Name) { it.Name = name; repaired = true; }
                var row = new TaskRow(it);
                Refresh(row);
                Rows.Add(row);
            }
            SetDirty(repaired);
            if (repaired) Notice?.Invoke("任务文件里有重复或缺失的名称/编号，已自动修复，请保存。");
        }

        public void Clear() { Load(null); }
        public IReadOnlyList<TaskItem> ToItems() { return Rows.Select(r => r.Item).ToList(); }
        public void MarkSaved() { SetDirty(false); }
        public TaskItem Snapshot(TaskRow row) { return Clone(row.Item); }

        // ───── 修改 ─────
        public TaskRow Add(string typeKey, int? index = null)
        {
            ITaskType t;
            if (!_types.TryGet(typeKey, out t)) throw new InvalidOperationException("未知任务类型：" + typeKey);
            Guard("Add", typeKey);

            var item = new TaskItem { Id = NewId(), Name = UniqueName(t.DisplayName, null), TypeKey = t.Key, Data = t.CreateDefaultData() };
            TaskRow row = Insert(item, index);
            _audit.Record(AuditCategory.TaskList, "Add", row.Name, newValue: t.Key);
            return row;
        }

        public TaskRow Paste(TaskItem template, int? index = null)
        {
            Guard("Paste", template.Name);
            TaskItem item = Clone(template);
            item.Id = NewId();
            item.Name = UniqueName((template.Name ?? "任务") + "_复制", null);
            TaskRow row = Insert(item, index);
            _audit.Record(AuditCategory.TaskList, "Paste", row.Name, newValue: row.TypeKey);
            return row;
        }

        public void Remove(TaskRow row)
        {
            if (row == null || !Rows.Contains(row)) return;
            Guard("Remove", row.Name);
            Rows.Remove(row);
            _audit.Record(AuditCategory.TaskList, "Remove", row.Name, Cut(row.TypeKey + "|" + row.Item.Data));   // 旧值留底，误删可追回
            SetDirty(true);
        }

        public void Move(TaskRow row, int newIndex)
        {
            int old = Rows.IndexOf(row);
            if (old < 0) return;
            newIndex = Math.Max(0, Math.Min(Rows.Count - 1, newIndex));
            if (old == newIndex) return;
            Guard("Move", row.Name);
            Rows.Move(old, newIndex);
            _audit.Record(AuditCategory.TaskList, "Move", row.Name, (old + 1).ToString(), (newIndex + 1).ToString());
            SetDirty(true);
        }

        public void Rename(TaskRow row, string name)
        {
            name = (name ?? "").Trim();
            if (row == null || name == row.Name) return;
            if (name.Length == 0) throw new InvalidOperationException("任务名称不能为空。");
            if (Exists(name, row)) throw new InvalidOperationException("任务名称已存在：" + name);
            Guard("Rename", row.Name);
            string old = row.Name;
            row.Item.Name = name; row.Refresh();
            _audit.Record(AuditCategory.TaskList, "Rename", name, old, name);
            SetDirty(true);
        }

        public void SetEnabled(TaskRow row, bool v) { Flag(row, "Enabled", v != row.Enabled, () => row.Item.Enabled = v, row.Enabled, v); }
        public void SetPauseAfter(TaskRow row, bool v) { Flag(row, "PauseAfter", v != row.PauseAfter, () => row.Item.PauseAfter = v, row.PauseAfter, v); }

        public void SetTimeout(TaskRow row, int ms)
        {
            if (ms < 0) throw new InvalidOperationException("超时不能为负数。");
            Flag(row, "TimeoutMs", ms != row.TimeoutMs, () => row.Item.TimeoutMs = ms, row.TimeoutMs, ms);
        }

        /// <summary>编辑界面改完参数后调用（经 ITaskEditSession）。数据没变化直接忽略，不产生审计。</summary>
        public void UpdateData(TaskRow row, string newData)
        {
            if (row == null || !Rows.Contains(row) || row.Item.Data == newData) return;
            Guard("Edit", row.Name);
            string old = row.Item.Data;
            row.Item.Data = newData; Refresh(row); row.Refresh();
            _audit.Record(AuditCategory.TaskList, "Edit", row.Name, Cut(old), Cut(newData), detail: row.TypeKey);
            SetDirty(true);
        }

        /// <summary>把任务转换成它声明的目标类型（不可逆）。列表不关心目标是什么类型。</summary>
        public void Convert(TaskRow row)
        {
            ITaskType t, target;
            if (!_types.TryGet(row.TypeKey, out t)) throw new InvalidOperationException("未知任务类型：" + row.TypeKey);
            var conv = t as ITaskConvertible;
            if (conv == null) throw new InvalidOperationException(t.DisplayName + " 不支持转换。");
            if (!_types.TryGet(conv.TargetTypeKey, out target)) throw new InvalidOperationException("目标任务类型未安装：" + conv.TargetTypeKey);
            Guard("Convert", row.Name);

            string data = conv.Convert(row.Item.Data);                 // 数据非法时抛异常，任务保持不变
            string oldType = row.TypeKey, oldData = row.Item.Data;
            row.Item.TypeKey = target.Key; row.Item.Data = data;
            Refresh(row); row.Refresh();
            _audit.Record(AuditCategory.TaskList, "Convert", row.Name, Cut(oldType + "|" + oldData), Cut(target.Key + "|" + data));
            SetDirty(true);
        }

        // ───── 内部 ─────
        private TaskRow Insert(TaskItem item, int? index)
        {
            var row = new TaskRow(item);
            Refresh(row);
            int i = index.HasValue ? Math.Max(0, Math.Min(Rows.Count, index.Value)) : Rows.Count;
            Rows.Insert(i, row);
            SetDirty(true);
            return row;
        }

        private void Flag(TaskRow row, string action, bool changed, Action set, object oldV, object newV)
        {
            if (row == null || !changed) return;
            Guard(action, row.Name);
            set(); row.Refresh();
            _audit.Record(AuditCategory.TaskList, action, row.Name, oldV.ToString(), newV.ToString());
            SetDirty(true);
        }

        private void Guard(string action, string target)
        {
            if (_locked) throw new InvalidOperationException("任务运行中，不能编辑。");
            if (_user.Role < EditRole)
            {
                _audit.Record(AuditCategory.TaskList, action, target, result: AuditResult.Denied, detail: "需要 " + EditRole + " 及以上角色");
                throw new InvalidOperationException("权限不足：编辑任务需要 " + EditRole + " 及以上角色。");
            }
        }

        private void Refresh(TaskRow row)
        {
            ITaskType t;
            bool known = _types.TryGet(row.TypeKey, out t);
            row.TypeName = known ? t.DisplayName : row.TypeKey;
            row.Summary = known ? t.Summary(row.Item.Data) : "（未知任务类型 " + row.TypeKey + "，是否缺少扩展包？）";
        }

        private bool Exists(string name, TaskRow except)
        { return Rows.Any(r => r != except && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)); }

        private string UniqueName(string baseName, TaskRow except)
        {
            baseName = (baseName ?? "").Trim();
            if (baseName.Length == 0) baseName = "任务";
            string n = baseName; int i = 2;
            while (Exists(n, except)) n = baseName + " " + i++;
            return n;
        }

        private static TaskItem Clone(TaskItem s)
        {
            return new TaskItem
            {
                Id = s.Id,
                Name = s.Name,
                TypeKey = s.TypeKey,
                Data = s.Data,
                Enabled = s.Enabled,
                PauseAfter = s.PauseAfter,
                TimeoutMs = s.TimeoutMs
            };
        }

        private static string NewId() { return Guid.NewGuid().ToString("N"); }
        private static string Cut(string s) { return s != null && s.Length > MaxAuditText ? s.Substring(0, MaxAuditText) + "…（已截断）" : s; }
        private void SetDirty(bool v) { if (IsDirty == v) return; IsDirty = v; DirtyChanged?.Invoke(this, EventArgs.Empty); }
    }
}

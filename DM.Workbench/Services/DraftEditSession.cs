using DM.Core.TaskList;
using DM.Workbench.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Workbench.Services
{
    /// <summary>
    /// 弹窗编辑用的"草稿会话"：编辑界面的 Commit 只写到内存里的草稿；
    /// 点"确定"才调用 Apply 写进任务列表（一次审计记录），点"取消"直接丢弃。
    /// 只读状态（权限不足、运行中）直接取自列表编辑器，并把变化事件转发给编辑界面。
    /// </summary>
    public sealed class DraftEditSession : ITaskEditSession
    {
        private readonly TaskListEditor _ed;
        private readonly TaskRow _row;
        private readonly ITaskType _type;
        private readonly string _original;
        private string _draft;

        public DraftEditSession(TaskListEditor ed, TaskRow row, ITaskType type)
        { _ed = ed; _row = row; _type = type; _original = row.Item.Data; }

        public string TaskId => _row.Id;
        public string Data => _draft ?? _original;
        public bool ReadOnly => _ed.IsReadOnly;

        public event EventHandler ReadOnlyChanged { add { _ed.ReadOnlyChanged += value; } remove { _ed.ReadOnlyChanged -= value; } }
        public event EventHandler ConvertRequested;

        public bool IsDirty => _draft != null && _draft != _original;
        public bool Applied { get; private set; }

        public void Commit(string newData) { _draft = newData; }               // 只改草稿，不碰任务列表，不抛异常
        public void Convert() { ConvertRequested?.Invoke(this, EventArgs.Empty); }   // 由弹窗确认后再执行

        /// <summary>参数校验结果，通过返回 null。</summary>
        public string Validate()
        {
            try { return _type.Validate(Data); }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>写进任务列表。权限不足、重名等错误会抛出，由弹窗显示。</summary>
        public void Apply()
        {
            if (!IsDirty) return;
            _ed.UpdateData(_row, _draft);
            Applied = true;
        }

        public void ApplyAndConvert()
        {
            Apply();
            _ed.Convert(_row);
            Applied = true;
        }
    }
}

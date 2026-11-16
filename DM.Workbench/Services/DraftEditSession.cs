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
    /// 弹窗编辑用的"草稿会话"：参数（编辑界面经 Commit 写入）和通用配置（Common）都先改草稿，
    /// 点"确定"才一次性写进任务列表，点"取消"直接丢弃。
    /// 应用顺序：先整体校验通用配置（失败什么都不改），再写参数，再写通用配置。
    /// </summary>
    public sealed class DraftEditSession : ITaskEditSession
    {
        private readonly TaskListEditor _ed;
        private readonly TaskRow _row;
        private readonly string _original;
        private string _draft;

        public DraftEditSession(TaskListEditor ed, TaskRow row, ITaskType type)
        { _ed = ed; _row = row; Type = type; _original = row.Item.Data; Common = new CommonDraft(row.Item); }

        public ITaskType Type { get; }
        public CommonDraft Common { get; }

        public string TaskId => _row.Id;
        public string Data => _draft ?? _original;
        public bool ReadOnly => _ed.IsReadOnly;

        public event EventHandler ReadOnlyChanged { add { _ed.ReadOnlyChanged += value; } remove { _ed.ReadOnlyChanged -= value; } }
        public event EventHandler ConvertRequested;

        public bool DataDirty => _draft != null && _draft != _original;
        public bool IsDirty => DataDirty || Common.IsChanged;
        public bool Applied { get; private set; }

        public void Commit(string newData) { _draft = newData; }                       // 只改草稿，不碰任务列表，不抛异常
        public void Convert() { ConvertRequested?.Invoke(this, EventArgs.Empty); }     // 由弹窗确认后再执行

        /// <summary>通用配置的硬性错误（名称为空或重复、超时为负）：有错不允许保存。</summary>
        public string CommonError() { return _ed.CheckName(_row, Common.Name) ?? TaskListEditor.CheckTimeout(Common.TimeoutMs); }

        /// <summary>参数校验结果（软性：用户可以选择仍然保存）。通过返回 null。</summary>
        public string Validate()
        {
            try { return Type.Validate(Data); }
            catch (Exception ex) { return ex.Message; }
        }

        public void Apply()
        {
            string ce = CommonError();
            if (ce != null) throw new InvalidOperationException(ce);

            if (DataDirty) _ed.UpdateData(_row, _draft);
            if (Common.IsChanged) _ed.ApplyCommon(_row, Common.Name, Common.Enabled, Common.PauseAfter, Common.TimeoutMs);
            if (IsDirty) Applied = true;
        }

        public void ApplyAndConvert()
        {
            Apply();
            _ed.Convert(_row);
            Applied = true;
        }
    }
}

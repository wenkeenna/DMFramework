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
    /// 选中任务时交给编辑界面的会话。Commit 在界面的定时器里被调用，绝不能抛异常（会崩进程），
    /// 所以编辑器抛出的任何错误都转成提示文字上报。
    /// </summary>
    public sealed class TaskEditSession : ITaskEditSession
    {
        private readonly TaskListEditor _ed;
        private readonly TaskRow _row;
        private readonly Action _converted;

        public TaskEditSession(TaskListEditor ed, TaskRow row, Action converted) { _ed = ed; _row = row; _converted = converted; }

        public string TaskId => _row.Id;
        public string Data => _row.Item.Data;
        public bool ReadOnly => _ed.IsReadOnly;

        // 转发事件而不是自己订阅：编辑界面取消订阅后不会遗留对会话的引用
        public event EventHandler ReadOnlyChanged { add { _ed.ReadOnlyChanged += value; } remove { _ed.ReadOnlyChanged -= value; } }

        public void Commit(string newData)
        {
            try { _ed.UpdateData(_row, newData); }
            catch (Exception ex) { _ed.Report(ex.Message); }
        }

        public void Convert()
        {
            try { _ed.Convert(_row); _converted(); }          // 转换后重新导航，右侧换成目标类型的编辑界面
            catch (Exception ex) { _ed.Report(ex.Message); }
        }
    }
}

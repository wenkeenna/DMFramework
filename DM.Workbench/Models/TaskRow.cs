using DM.Core.TaskList;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Workbench.Models
{
    public enum TaskRunState { Idle, Running, Success, Failed, Cancelled }

    /// <summary>列表里的一行 = 持久化数据 TaskItem + 运行状态。运行状态不进文件。</summary>
    public sealed class TaskRow : BindableBase
    {
        private int _order;
        private string _summary = "", _typeName = "", _error;
        private TaskRunState _state;
        private TimeSpan _time;

        public TaskRow(TaskItem item) { Item = item; }
        public TaskItem Item { get; }

        public string Id => Item.Id;
        public string Name => Item.Name;
        public string TypeKey => Item.TypeKey;
        public bool Enabled => Item.Enabled;
        public bool PauseAfter => Item.PauseAfter;
        public int TimeoutMs => Item.TimeoutMs;

        public int Order { get { return _order; } set { SetProperty(ref _order, value); } }
        public string Summary { get { return _summary; } set { SetProperty(ref _summary, value ?? ""); } }
        public string TypeName { get { return _typeName; } set { SetProperty(ref _typeName, value ?? ""); } }
        public TaskRunState State { get { return _state; } set { SetProperty(ref _state, value); } }
        public string LastError { get { return _error; } set { SetProperty(ref _error, value); } }

        public TimeSpan ExecutionTime
        {
            get { return _time; }
            set { if (SetProperty(ref _time, value)) RaisePropertyChanged(nameof(ExecutionTimeText)); }
        }
        public string ExecutionTimeText => _time.ToString(@"hh\:mm\:ss\.fff");

        public void Refresh() { RaisePropertyChanged(string.Empty); }        // 数据改变后通知全部属性
        public void ResetRun() { State = TaskRunState.Idle; ExecutionTime = TimeSpan.Zero; LastError = null; }
    }
}

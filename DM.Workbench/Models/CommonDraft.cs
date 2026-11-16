using DM.Core.TaskList;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Workbench.Models
{
    /// <summary>通用配置的草稿。界面绑定它；点"确定"才写回任务。IsChanged 是相对打开窗口时的原始值。</summary>
    public sealed class CommonDraft : BindableBase
    {
        private readonly string _n; private readonly bool _e, _p; private readonly int _t;
        private string _name; private bool _enabled, _pause; private int _timeout;

        public CommonDraft(TaskItem i)
        {
            _n = i.Name ?? ""; _e = i.Enabled; _p = i.PauseAfter; _t = i.TimeoutMs;
            _name = _n; _enabled = _e; _pause = _p; _timeout = _t;
        }

        public string Name { get { return _name; } set { SetProperty(ref _name, value ?? ""); } }
        public bool Enabled { get { return _enabled; } set { SetProperty(ref _enabled, value); } }
        public bool PauseAfter { get { return _pause; } set { SetProperty(ref _pause, value); } }
        public int TimeoutMs { get { return _timeout; } set { SetProperty(ref _timeout, value); } }

        public bool IsChanged => Name.Trim() != _n || Enabled != _e || PauseAfter != _p || TimeoutMs != _t;
    }
}

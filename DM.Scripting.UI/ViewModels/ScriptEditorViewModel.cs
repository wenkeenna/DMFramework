using DM.Core.Scripting;
using DM.Core.TaskList;
using DM.Scripting.Services;
using DM.UI;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;

namespace DM.Scripting.UI.ViewModels
{
    /// <summary>
    /// 1. 输入停顿 400ms 后才编译检查并写回任务（防抖），打字不卡。
    /// 2. 有语法错误也写回（半成品脚本要能存），运行前编译会拦住。
    /// 3. 切换任务或离开时若有未写回的修改，先写回，不丢字。
    /// 4. 会话只读（运行中、权限不足）时编辑器只读；恢复可写时若有未写回内容，自动写回。
    /// </summary>
    public sealed class ScriptEditorViewModel : BindableBase, INavigationAware
    {
        private readonly DispatcherTimer _timer;
        private ITaskEditSession _session;
        private string _text = "", _status = "";
        private bool _loading, _dirty, _readOnly = true;
        private ScriptError _selectedError;

        public ScriptEditorViewModel(IScriptCommandRegistry registry, ScriptCompletion completion)
        {
            Registry = registry; Completion = completion;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _timer.Tick += (s, e) => { _timer.Stop(); Check(); Commit(); };

            var view = new ListCollectionView(registry.All.Select(c => c.Spec).OrderBy(s => s.Group).ThenBy(s => s.Name).ToList());
            view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            Commands = view;
            InsertCommand = new DelegateCommand<ScriptCommandSpec>(s => { if (s != null && !ReadOnly) InsertRequested?.Invoke(s.Name + " "); });
        }

        public ScriptCommandRegistry Registry { get; }
        public ScriptCompletion Completion { get; }
        public ICollectionView Commands { get; }
        public ObservableCollection<ScriptError> Errors { get; } = new ObservableCollection<ScriptError>();
        public DelegateCommand<ScriptCommandSpec> InsertCommand { get; }

        public event Action<string> InsertRequested;          // 视图订阅：在光标处插入
        public event Action<int> JumpToLine;                  // 视图订阅：跳到某行

        public string Text
        {
            get { return _text; }
            set
            {
                if (!SetProperty(ref _text, value ?? "") || _loading) return;
                _dirty = true; _timer.Stop(); _timer.Start();
            }
        }
        public string Status { get { return _status; } private set { SetProperty(ref _status, value); } }
        public bool ReadOnly { get { return _readOnly; } private set { SetProperty(ref _readOnly, value); } }

        public ScriptError SelectedError
        {
            get { return _selectedError; }
            set { if (SetProperty(ref _selectedError, value) && value != null) JumpToLine?.Invoke(value.Line); }
        }

        // ───── 导航 ─────
        public bool IsNavigationTarget(NavigationContext c) { return true; }       // 复用同一个实例
        public void OnNavigatedFrom(NavigationContext c) { _timer.Stop(); Commit(); }

        public void OnNavigatedTo(NavigationContext c)
        {
            _timer.Stop(); Commit();                                                // 先写回上一个任务
            if (_session != null) _session.ReadOnlyChanged -= OnReadOnlyChanged;

            _session = c.Parameters.ContainsKey("Session") ? c.Parameters.GetValue<ITaskEditSession>("Session") : null;
            _loading = true;
            try { Text = _session == null ? "" : (_session.Data ?? ""); } finally { _loading = false; }
            _dirty = false;

            if (_session == null) { ReadOnly = true; Errors.Clear(); Status = "未选中任务。"; return; }
            _session.ReadOnlyChanged += OnReadOnlyChanged;
            ReadOnly = _session.ReadOnly;
            Check();
        }

        private void OnReadOnlyChanged(object s, EventArgs e)
        {
            UiThread.Invoke(() =>
            {
                ReadOnly = _session == null || _session.ReadOnly;
                if (!ReadOnly && _dirty) Commit();
            });
        }

        private void Commit()
        {
            if (!_dirty || _session == null || _session.ReadOnly) return;
            _session.Commit(Text);                         // 标脏、审计、重名检查由列表一侧负责；Commit 不抛异常
            _dirty = false;
        }

        private void Check()
        {
            CompileResult r = new ScriptCompiler(Registry).Compile(Text);
            Errors.Clear();
            foreach (ScriptError e in r.Errors) Errors.Add(e);
            Status = r.Ok ? "语法检查通过，共 " + r.Lines.Count + " 条命令" : r.Errors.Count + " 处错误";
        }
    }
}

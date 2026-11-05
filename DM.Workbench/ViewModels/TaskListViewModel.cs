using DM.Core.Events;
using DM.Core.Projects;
using DM.Core.TaskList;
using DM.Workbench.Models;
using DM.Workbench.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using DM.Core.Identity;
using DM.UI;

namespace DM.Workbench.ViewModels
{
    /// <summary>
    /// 任务列表主 ViewModel，注册为单例。
    /// 选中任务 → 通过区域导航把 ITaskEditSession 交给右侧编辑界面；运行时程序自动选中当前任务，不导航（避免频繁切换），
    /// 运行结束后若选中项与编辑界面不一致，再统一导航一次。
    /// </summary>
    public sealed class TaskListViewModel : BindableBase
    {
        private const string DetailRegion = "TaskDetailRegion";

        private readonly TaskListEditor _editor;
        private readonly ITaskRunner _runner;
        private readonly IProjectLifecycle _life;
        private readonly IProjectCatalog _catalog;
        private readonly ILastProjectStore _last;
        private readonly ITaskTypeRegistry _types;
        private readonly ICurrentUser _user;
        private readonly IDialogService _dialogs;
        private readonly IRegionManager _regions;
        private readonly List<DelegateCommand> _cmds = new List<DelegateCommand>();
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        private readonly Stopwatch _sw = new Stopwatch();
        private TaskRow _selected, _running, _navigated;
        private TaskItem _clip;
        private string _message = "";
        private bool _auto;

        public TaskListViewModel(TaskListEditor editor, ITaskRunner runner, IProjectLifecycle life, IProjectCatalog catalog,
                                 ILastProjectStore last, ITaskTypeRegistry types, ICurrentUser user, IDialogService dialogs,
                                 IRegionManager regions, IEventAggregator events)
        {
            _editor = editor; _runner = runner; _life = life; _catalog = catalog; _last = last;
            _types = types; _user = user; _dialogs = dialogs; _regions = regions;

            AddTaskCommand = Cmd(OnAdd, () => CanEdit && ProjectOpen);
            DeleteTaskCommand = Cmd(OnDelete, () => CanEdit && SelectedTask != null);
            MoveUpCommand = Cmd(() => MoveTo(_selected, Index - 1), () => CanEdit && Index > 0);
            MoveDownCommand = Cmd(() => MoveTo(_selected, Index + 1), () => CanEdit && Index >= 0 && Index < Rows.Count - 1);
            CopyTaskCommand = Cmd(OnCopy, () => SelectedTask != null);
            PasteTaskCommand = Cmd(OnPaste, () => CanEdit && ProjectOpen && _clip != null);

            RunFlowCommand = Cmd(() => Run(Rows.ToList(), false), () => CanRun && Rows.Count > 0);
            RunSelectedTaskCommand = Cmd(OnRunSelected, () => CanRun && SelectedTask != null);
            NextStepCommand = Cmd(OnNext, () => CanRun && Rows.Count > 0);
            LoopRunCommand = Cmd(() => Run(Rows.ToList(), true), () => CanRun && Rows.Count > 0);
            StopFlowCommand = Cmd(() => { Message = "正在停止…"; _runner.Stop("用户点击停止"); }, () => IsRunning);
            ResetStateCommand = Cmd(() => { foreach (TaskRow r in Rows) r.ResetRun(); Message = "任务状态已重置。"; }, () => !IsRunning);

            NewProjectCommand = Cmd(OnNewProject, () => !IsRunning && _user.Role >= AccessRole.Engineer);
            OpenProjectCommand = Cmd(OnOpenProject, () => !IsRunning);
            SaveProjectCommand = Cmd(OnSave, () => ProjectOpen);

            _timer.Tick += (s, e) => { if (_running != null) _running.ExecutionTime = _sw.Elapsed; };

            _runner.TaskStarted += OnTaskStarted;
            _runner.TaskFinished += OnTaskFinished;
            _runner.RunningChanged += (s, e) =>
            {
                _editor.IsLocked = _runner.IsRunning;
                Changed();
                if (!_runner.IsRunning && _selected != _navigated) Navigate();      // 运行期间选中项变了，结束后让编辑界面跟上
            };
            _editor.ReadOnlyChanged += (s, e) => Changed();
            _editor.DirtyChanged += (s, e) => RaisePropertyChanged(nameof(IsProjectSaved));
            _editor.Notice += m => Message = m;
            _editor.Rows.CollectionChanged += (s, e) =>
            {
                if (_selected != null && !Rows.Contains(_selected)) SelectedTask = null;
                RaiseAll();
            };
            events.GetEvent<ProjectOpenedEvent>().Subscribe(c => Changed(), ThreadOption.UIThread, true);
            events.GetEvent<ProjectClosedEvent>().Subscribe(c => Changed(), ThreadOption.UIThread, true);

            Message = ProjectOpen ? "" : "请先新建或打开项目。";
        }

        // ───── 绑定属性 ─────
        public ObservableCollection<TaskRow> TaskList => _editor.Rows;
        private ObservableCollection<TaskRow> Rows => _editor.Rows;
        public string ActiveProjectName => _life.Current == null ? "（无）" : _life.Current.Name;
        public bool IsProjectSaved => !_editor.IsDirty;        // 只反映任务列表；参数在"保存"或关闭项目时一并落盘
        public bool IsRunning => _runner.IsRunning;
        public bool CanEdit => !_editor.IsReadOnly;
        public string Message { get { return _message; } set { SetProperty(ref _message, value ?? ""); } }

        public TaskRow SelectedTask
        {
            get { return _selected; }
            set
            {
                if (!SetProperty(ref _selected, value)) return;
                RaiseSelectionProps(); RaiseAll();
                if (!_auto) Navigate();
            }
        }

        // 选中任务的属性区：改动经编辑器校验和审计；被拒绝时异步刷新，让文本框回显旧值
        public string SelName { get { return _selected == null ? "" : _selected.Name; } set { Edit(() => _editor.Rename(_selected, value)); } }
        public bool SelEnabled { get { return _selected != null && _selected.Enabled; } set { Edit(() => _editor.SetEnabled(_selected, value)); } }
        public bool SelPauseAfter { get { return _selected != null && _selected.PauseAfter; } set { Edit(() => _editor.SetPauseAfter(_selected, value)); } }
        public int SelTimeoutMs { get { return _selected == null ? 0 : _selected.TimeoutMs; } set { Edit(() => _editor.SetTimeout(_selected, value)); } }
        public string SelTypeName => _selected == null ? "" : _selected.TypeName;

        public DelegateCommand AddTaskCommand { get; }
        public DelegateCommand DeleteTaskCommand { get; }
        public DelegateCommand MoveUpCommand { get; }
        public DelegateCommand MoveDownCommand { get; }
        public DelegateCommand CopyTaskCommand { get; }
        public DelegateCommand PasteTaskCommand { get; }
        public DelegateCommand RunFlowCommand { get; }
        public DelegateCommand RunSelectedTaskCommand { get; }
        public DelegateCommand NextStepCommand { get; }
        public DelegateCommand LoopRunCommand { get; }
        public DelegateCommand StopFlowCommand { get; }
        public DelegateCommand ResetStateCommand { get; }
        public DelegateCommand NewProjectCommand { get; }
        public DelegateCommand OpenProjectCommand { get; }
        public DelegateCommand SaveProjectCommand { get; }

        private bool ProjectOpen => _life.Current != null;
        private bool CanRun => !IsRunning && ProjectOpen;
        private int Index => _selected == null ? -1 : Rows.IndexOf(_selected);

        // ───── 编辑 ─────
        private void OnAdd()
        {
            _dialogs.ShowDialog("TaskTypePickerDialog", r =>
            {
                if (r.Result != ButtonResult.OK) return;
                try
                {
                    TaskRow row = _editor.Add(r.Parameters.GetValue<string>("TypeKey"), _selected == null ? (int?)null : Index + 1);
                    SelectedTask = row; Message = "已添加：" + row.Name;
                }
                catch (Exception ex) { Message = ex.Message; }
            });
        }

        private void OnDelete()
        {
            TaskRow row = _selected; int i = Index;
            if (MessageBox.Show("确定删除任务“" + row.Name + "”吗？", "确认", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            try
            {
                _editor.Remove(row);
                SelectedTask = i < Rows.Count ? Rows[i] : Rows.LastOrDefault();
                Message = "已删除：" + row.Name;
            }
            catch (Exception ex) { Message = ex.Message; }
        }

        /// <summary>上移、下移、拖拽排序的统一入口。</summary>
        public void MoveTo(TaskRow row, int index)
        {
            try { _editor.Move(row, index); RaiseAll(); }
            catch (Exception ex) { Message = ex.Message; }
        }

        private void OnCopy() { _clip = _editor.Snapshot(_selected); Message = "已复制：" + _clip.Name; RaiseAll(); }

        private void OnPaste()
        {
            try { TaskRow row = _editor.Paste(_clip, _selected == null ? (int?)null : Index + 1); SelectedTask = row; Message = "已粘贴：" + row.Name; }
            catch (Exception ex) { Message = ex.Message; }
        }

        private void Edit(Action a)
        {
            if (_selected == null) return;
            try { a(); } catch (Exception ex) { Message = ex.Message; }
            UiThread.Post(RaiseSelectionProps);
        }

        // ───── 运行 ─────
        private void OnRunSelected()
        {
            if (!_selected.Enabled) { Message = "该任务已禁用。"; return; }
            Run(new List<TaskRow> { _selected }, false);
        }

        private void OnNext()
        {
            int next = Index + 1;
            if (next >= Rows.Count) { Message = "已经是最后一步。"; return; }
            TaskRow row = Rows[next];
            AutoSelect(row);
            if (!row.Enabled) { Message = "“" + row.Name + "”已禁用，已跳过。"; return; }
            Run(new List<TaskRow> { row }, false);
        }

        private async void Run(List<TaskRow> rows, bool loop)
        {
            if (!CanRun) return;
            foreach (TaskRow r in rows) r.ResetRun();
            Message = loop ? "循环运行中…" : "运行中…";
            try
            {
                RunReport rep = await _runner.RunAsync(rows.Select(r => r.Item).ToList(), loop);
                if (rep.Outcome == RunOutcome.Failed && rep.FailedTaskId != null)
                { TaskRow bad = Find(rep.FailedTaskId); if (bad != null) AutoSelect(bad); }
                Message = Describe(rep, loop);
            }
            catch (Exception ex) { Message = "运行异常：" + ex.Message; }
        }

        private static string Describe(RunReport r, bool loop)
        {
            switch (r.Outcome)
            {
                case RunOutcome.Completed: return loop ? "循环结束，共 " + r.Cycles + " 轮。" : "运行完成，用时 " + r.Elapsed.TotalSeconds.ToString("F1") + " 秒。";
                case RunOutcome.Paused: return "已暂停（任务设置了“执行后暂停”），点“下一步”继续。";
                case RunOutcome.Cancelled: return "已停止。";
                default: return "运行失败：" + r.Error;
            }
        }

        private void OnTaskStarted(object s, TaskStartedEventArgs e)
        {
            TaskRow row = Find(e.TaskId);
            if (row == null) return;
            row.State = TaskRunState.Running; row.LastError = null; row.ExecutionTime = TimeSpan.Zero;
            _running = row; _sw.Restart(); _timer.Start();
            AutoSelect(row);
        }

        private void OnTaskFinished(object s, TaskFinishedEventArgs e)
        {
            _timer.Stop();
            TaskRow row = Find(e.TaskId);
            _running = null;
            if (row == null) return;
            row.State = e.Cancelled ? TaskRunState.Cancelled : e.Success ? TaskRunState.Success : TaskRunState.Failed;
            row.ExecutionTime = e.Elapsed; row.LastError = e.Error;
        }

        // ───── 项目 ─────
        private void OnNewProject()
        {
            _dialogs.ShowDialog("NewProjectDialog", r =>
            { if (r.Result == ButtonResult.OK) OpenProjectAsync(r.Parameters.GetValue<string>("ProjectName")); });
        }

        private void OnOpenProject()
        {
            _dialogs.ShowDialog("OpenProjectDialog", r =>
            { if (r.Result == ButtonResult.OK) OpenProjectAsync(r.Parameters.GetValue<string>("ProjectName")); });
        }

        private async void OpenProjectAsync(string name)
        {
            if (IsRunning) return;
            ProjectInfo info = _catalog.Find(name);
            if (info == null) { Message = "项目不存在：" + name; return; }
            try
            {
                Message = "正在打开项目…";
                ProjectOpenResult res = await _life.OpenAsync(info.Name, info.Directory);    // 先关闭旧项目（任务列表脏则自动保存）
                if (res.Aborted) { Message = "项目打开失败：" + FailText(res); return; }
                _last.Write(info.Name);
                Message = res.HasWarnings ? "项目已打开，但部分模块失败：" + FailText(res) : "已打开项目：" + info.Name;
            }
            catch (Exception ex) { Message = "项目打开失败：" + ex.Message; }
            Changed();
        }

        private async void OnSave()
        {
            try
            {
                IReadOnlyList<ProjectAwareFailure> fails = await _life.SaveAsync();
                Message = fails.Count == 0 ? "已保存。" : "保存失败：" + string.Join("；", fails.Select(f => f.Name + "：" + f.Error.Message));
            }
            catch (Exception ex) { Message = "保存失败：" + ex.Message; }
        }

        private static string FailText(ProjectOpenResult r) { return string.Join("；", r.Failures.Select(f => f.Name + "：" + f.Error.Message)); }

        // ───── 右侧编辑界面导航 ─────
        private void Navigate()
        {
            _navigated = _selected;
            ITaskType t;
            if (_selected == null || !_types.TryGet(_selected.TypeKey, out t) || string.IsNullOrEmpty(t.EditorView))
            { Go("EmptyTaskView", new NavigationParameters()); return; }      // 必须清空旧界面，否则会误改上一个任务

            var session = new TaskEditSession(_editor, _selected, Navigate);
            Go(t.EditorView, new NavigationParameters { { "Session", session } });
        }

        private void Go(string view, NavigationParameters p)
        {
            try
            {
                _regions.RequestNavigate(DetailRegion, view, r =>
                { if (r.Result != true) Message = "无法打开编辑界面 " + view + "：" + (r.Error == null ? "" : r.Error.Message); }, p);
            }
            catch (Exception ex) { Message = "无法打开编辑界面 " + view + "：" + ex.Message; }
        }

        // ───── 工具 ─────
        private TaskRow Find(string id) { return Rows.FirstOrDefault(r => r.Id == id); }
        private void AutoSelect(TaskRow row) { _auto = true; SelectedTask = row; _auto = false; }
        private DelegateCommand Cmd(Action exec, Func<bool> can) { var c = new DelegateCommand(exec, can); _cmds.Add(c); return c; }
        private void RaiseAll() { foreach (DelegateCommand c in _cmds) c.RaiseCanExecuteChanged(); }

        private void RaiseSelectionProps()
        {
            foreach (string n in new[] { nameof(SelName), nameof(SelEnabled), nameof(SelPauseAfter), nameof(SelTimeoutMs), nameof(SelTypeName) })
                RaisePropertyChanged(n);
        }

        private void Changed()
        {
            RaisePropertyChanged(nameof(IsRunning)); RaisePropertyChanged(nameof(CanEdit));
            RaisePropertyChanged(nameof(ActiveProjectName)); RaisePropertyChanged(nameof(IsProjectSaved));
            RaiseAll();
        }
    }
}

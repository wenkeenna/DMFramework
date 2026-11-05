using DM.Core.TaskList;
using DM.Engine.TaskList;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DM.UI.TaskEditing
{
    /// <summary>
    /// 专用任务编辑器基类。界面直接绑定 Data.xxx，改动自动：
    ///   1. 立即刷新校验提示（和转换预览）
    ///   2. 停顿 300ms 后写回任务（Commit）；切换任务或离开时立即写回，不丢改动
    /// 运行中或权限不足时会话只读，IsEditable=false，界面整体禁用。
    /// 载入时数据损坏：提示并载入默认值，但在用户改动之前不会写回，不会悄悄覆盖原数据。
    /// 任务类型实现了 ITaskConvertible 且目标类型已安装时，才提供转换预览和转换按钮。
    /// </summary>
    public abstract class TaskEditorViewModelBase<TData> : BindableBase, INavigationAware where TData : BindableBase, new()
    {
        private readonly ITaskType _type;
        private readonly ITaskConvertible _conv;
        private readonly DispatcherTimer _timer;
        private ITaskEditSession _session;
        private ChangeTracker _tracker;
        private TData _data = new TData();
        private bool _loading, _dirty;
        private string _preview = "", _error = "", _loadWarning;

        protected TaskEditorViewModelBase(ITaskTypeRegistry types, string typeKey)
        {
            if (!types.TryGet(typeKey, out _type)) throw new InvalidOperationException("任务类型未注册：" + typeKey);

            ITaskType target = null;
            _conv = _type as ITaskConvertible;
            if (_conv != null && !types.TryGet(_conv.TargetTypeKey, out target)) _conv = null;     // 目标类型没安装就不提供转换
            ConvertTitle = target == null ? "" : "转换为“" + target.DisplayName + "”后的内容（只读）";
            ConvertButtonText = target == null ? "" : "转为" + target.DisplayName;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _timer.Tick += (s, e) => Flush();
            ConvertCommand = new DelegateCommand(DoConvert, () => IsEditable && _conv != null).ObservesProperty(() => IsEditable);
        }

        public TData Data { get { return _data; } }
        public bool IsEditable { get { return _session != null && !_session.ReadOnly; } }
        public bool Convertible { get { return _conv != null; } }
        public string ConvertTitle { get; }
        public string ConvertButtonText { get; }
        public string Preview { get { return _preview; } private set { SetProperty(ref _preview, value); } }
        public string Error { get { return _error; } private set { SetProperty(ref _error, value); } }
        public DelegateCommand ConvertCommand { get; }

        /// <summary>数据载入后调用，子类可重写（例如设置默认选中项）。</summary>
        protected virtual void OnLoaded() { }

        // ───── 导航 ─────
        public bool IsNavigationTarget(NavigationContext c) { return true; }          // 复用同一个实例
        public void OnNavigatedFrom(NavigationContext c) { Flush(); }

        public void OnNavigatedTo(NavigationContext c)
        {
            Flush();                                                                   // 先写回上一个任务
            if (_session != null) _session.ReadOnlyChanged -= OnReadOnlyChanged;
            if (_tracker != null) { _tracker.Dispose(); _tracker = null; }

            _session = c.Parameters.ContainsKey("Session") ? c.Parameters.GetValue<ITaskEditSession>("Session") : null;
            _loading = true;
            try
            {
                _loadWarning = null;
                try { _data = TaskJson.Deserialize<TData>(_session == null ? null : _session.Data); }
                catch (InvalidOperationException ex) { _data = new TData(); _loadWarning = ex.Message + "（已载入默认值，修改后将覆盖原数据）"; }
                RaisePropertyChanged(nameof(Data));
                OnLoaded();
                _tracker = new ChangeTracker(_data, Touch);
            }
            finally { _loading = false; }

            _dirty = false;
            if (_session != null) _session.ReadOnlyChanged += OnReadOnlyChanged;
            RaisePropertyChanged(nameof(IsEditable));
            RefreshPreview();
        }

        private void OnReadOnlyChanged(object s, EventArgs e) { UiThread.Invoke(() => RaisePropertyChanged(nameof(IsEditable))); }

        private void Touch()
        {
            if (_loading) return;
            _dirty = true; _loadWarning = null;
            RefreshPreview();
            _timer.Stop(); _timer.Start();
        }

        private void Flush()
        {
            _timer.Stop();
            if (!_dirty || _session == null || _session.ReadOnly) return;
            _session.Commit(TaskJson.Serialize(_data));                                // 标脏、审计、重名检查由列表一侧负责
            _dirty = false;
        }

        private void RefreshPreview()
        {
            string json = TaskJson.Serialize(_data), converted = "";
            if (_conv != null) { try { converted = _conv.Convert(json); } catch (InvalidOperationException) { } }
            string err = _type.Validate(json);
            Preview = converted;
            Error = (_loadWarning == null ? "" : _loadWarning + "\n") + (err ?? "");
        }

        private void DoConvert() { Flush(); if (_session != null) _session.Convert(); }
    }
}

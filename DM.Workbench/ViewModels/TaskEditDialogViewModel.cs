using DM.Core.TaskList;
using DM.Workbench.Services;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DM.Workbench.ViewModels
{
    /// <summary>
    /// 任务编辑弹窗：按任务类型的 EditorView 名字从容器创建编辑界面，交给它一个草稿会话。
    /// 编辑界面每次都是新实例（弹窗关闭即释放），不会残留上一个任务的状态。
    /// </summary>
    public sealed class TaskEditDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IContainerProvider _container;
        private DraftEditSession _session;
        private ITaskEditor _editor;
        private object _view;
        private string _title = "编辑任务", _message = "";
        private bool _readOnly, _allowClose;

        public TaskEditDialogViewModel(IContainerProvider container)
        {
            _container = container;
            OkCommand = new DelegateCommand(OnOk, () => !ReadOnly).ObservesProperty(() => ReadOnly);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        public string Title { get { return _title; } private set { SetProperty(ref _title, value); } }
        public event Action<IDialogResult> RequestClose;

        public object Editor { get { return _view; } private set { SetProperty(ref _view, value); } }
        public string Message { get { return _message; } private set { SetProperty(ref _message, value); } }
        public bool ReadOnly
        {
            get { return _readOnly; }
            private set { if (SetProperty(ref _readOnly, value)) { RaisePropertyChanged(nameof(CanApply)); RaisePropertyChanged(nameof(CancelText)); } }
        }
        public bool CanApply => !_readOnly;
        public string CancelText => _readOnly ? "关闭" : "取消";
        public DelegateCommand OkCommand { get; }
        public DelegateCommand CancelCommand { get; }

        // ───── 确定 / 取消 ─────
        private void OnOk()
        {
            if (_session == null) return;
            try
            {
                if (_editor != null) _editor.Flush();                       // 把 300ms 防抖里还没提交的改动先放进草稿
                string err = _session.Validate();
                if (err != null && !Confirm("参数校验没有通过：\n" + err + "\n\n仍要保存吗？（运行前预检仍会拦住这个任务）")) return;
                _session.Apply();
                Close(false);
            }
            catch (Exception ex) { Message = ex.Message; }                  // 权限不足、重名等：留在窗口里
        }

        private void OnCancel()
        {
            if (!ReadOnly && !ConfirmDiscard()) return;
            Close(false, cancel: true);
        }

        private bool ConfirmDiscard()
        {
            if (_editor != null) _editor.Flush();
            return _session == null || !_session.IsDirty || Confirm("放弃未保存的修改吗？");
        }

        private void OnConvertRequested(object s, EventArgs e)
        {
            if (!Confirm("转换后不可逆，之后按目标类型的方式编辑。\n确定转换吗？")) return;
            try
            {
                if (_editor != null) _editor.Flush();
                _session.ApplyAndConvert();
                Close(true);
            }
            catch (Exception ex) { Message = ex.Message; }
        }

        private void Close(bool converted, bool cancel = false)
        {
            _allowClose = true;
            RequestClose?.Invoke(new DialogResult(cancel ? ButtonResult.Cancel : ButtonResult.OK,
                new DialogParameters { { "Converted", converted } }));
        }

        private static bool Confirm(string text)
        { return MessageBox.Show(text, "编辑任务", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes; }

        // ───── IDialogAware ─────
        public bool CanCloseDialog()
        {
            if (_allowClose || _session == null || ReadOnly) return true;          // 点右上角 × 或按 Esc 也走这里
            return ConfirmDiscard();
        }

        public void OnDialogOpened(IDialogParameters p)
        {
            _session = p.GetValue<DraftEditSession>("Session");
            Title = p.GetValue<string>("Title");
            ReadOnly = _session.ReadOnly;
            _session.ReadOnlyChanged += OnReadOnlyChanged;
            _session.ConvertRequested += OnConvertRequested;

            string viewName = p.GetValue<string>("View");
            try
            {
                var view = _container.Resolve<object>(viewName) as FrameworkElement;      // 与 RegisterForNavigation 注册的名字一致
                _editor = view == null ? null : view.DataContext as ITaskEditor;
                if (_editor == null) throw new InvalidOperationException("编辑界面 " + viewName + " 的 ViewModel 没有实现 ITaskEditor。");
                Editor = view;
                _editor.Attach(_session);
            }
            catch (Exception ex) { Message = "无法创建编辑界面：" + ex.Message; }
        }

        private void OnReadOnlyChanged(object s, EventArgs e)
        { if (_session != null) ReadOnly = _session.ReadOnly; }

        public void OnDialogClosed()
        {
            if (_session != null) { _session.ReadOnlyChanged -= OnReadOnlyChanged; _session.ConvertRequested -= OnConvertRequested; }
            if (_editor != null) { _editor.Detach(); _editor = null; }      // 解除订阅，编辑界面才能被回收
            Editor = null;
        }
    }
}

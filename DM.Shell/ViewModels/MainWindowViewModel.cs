using DM.Core.Actions;
using DM.Core.Audit;
using DM.Core.Devices;
using DM.Core.Events;
using DM.Core.Identity;
using DM.Core.Projects;
using DM.Core.TaskList;
using DM.UI;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DM.Shell.ViewModels
{
    public sealed class DeviceStatusRow : BindableBase
    {
        private DeviceState _state; private string _error;
        public DeviceStatusRow(string name) { Name = name; }
        public string Name { get; }
        public DeviceState State { get { return _state; } set { SetProperty(ref _state, value); } }
        public string Error { get { return _error; } set { SetProperty(ref _error, value); } }
    }

    public sealed class MainWindowViewModel : BindableBase
    {
        private static readonly HashSet<string> Infra = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Log", "Audit" };    // 正常时不在状态栏显示

        private readonly ShellOptions _options;
        private readonly IIdentityService _identity;
        private readonly ICurrentUser _user;
        private readonly IProjectLifecycle _life;
        private readonly ITaskRunner _runner;
        private readonly IDialogService _dialogs;
        private readonly LoginFlow _login;
        private readonly IContainerProvider _container;
        private readonly IEventAggregator _events;
        private readonly IAuditService _audit;
        private readonly List<DelegateCommand> _cmds = new List<DelegateCommand>();
        private bool _emergency;
        private string _emergencyText = "";

        public MainWindowViewModel(ShellOptions options, IIdentityService identity, ICurrentUser user, IProjectLifecycle life,
                                   ITaskRunner runner, IDialogService dialogs, LoginFlow login, IContainerProvider container,
                                   IEventAggregator events, IAuditService audit, IDeviceHub hub, ISafetyState safety)
        {
            _options = options; _identity = identity; _user = user; _life = life; _runner = runner; _dialogs = dialogs;
            _login = login; _container = container; _events = events; _audit = audit;

            SwitchUserCommand = Cmd(OnSwitchUser, () => !_runner.IsRunning);
            ChangePasswordCommand = Cmd(() => _dialogs.ShowDialog("ChangePasswordDialog"), () => _user.IsAuthenticated);
            AccountManagementCommand = Cmd(() => _dialogs.ShowDialog("AccountManagementDialog"), () => _user.Role == AccessRole.Administrator);
            AuditViewCommand = Cmd(() => _dialogs.ShowDialog("AuditViewDialog"), () => _user.Role >= AccessRole.Engineer);
            EmergencyStopCommand = Cmd(OnEmergencyStop, () => !_emergency);
            ResetEmergencyCommand = Cmd(OnResetEmergency, () => _emergency);
            ExitCommand = new DelegateCommand(() => { if (Application.Current.MainWindow != null) Application.Current.MainWindow.Close(); });

            _identity.SessionChanged += (s, e) => UiThread.Invoke(Refresh);
            _runner.RunningChanged += (s, e) => UiThread.Invoke(Refresh);
            hub.StateChanged += (s, e) => UiThread.Invoke(() => OnDeviceState(e));
            events.GetEvent<ProjectOpenedEvent>().Subscribe(c => Refresh(), ThreadOption.UIThread, true);
            events.GetEvent<ProjectClosedEvent>().Subscribe(c => Refresh(), ThreadOption.UIThread, true);
            events.GetEvent<EmergencyStopEvent>().Subscribe(OnEmergencyEvent, ThreadOption.UIThread, true);

            if (safety.IsEmergencyStopped) { _emergency = true; _emergencyText = safety.Reason ?? ""; }
        }

        public ObservableCollection<DeviceStatusRow> Devices { get; } = new ObservableCollection<DeviceStatusRow>();
        public string WindowTitle => _options.Title + (_life.Current == null ? "" : " — 项目：" + _life.Current.Name);
        public string ProjectText => _life.Current == null ? "未打开项目" : "项目：" + _life.Current.Name;
        public string IdentityText
        {
            get
            {
                UserSession s = _identity.Current;
                return s == null ? "未登录" : "用户：" + s.Username + "（" + RoleName(s.Role) + "）";
            }
        }
        public bool IsEmergency => _emergency;
        public string EmergencyText => _emergencyText;

        public DelegateCommand SwitchUserCommand { get; }
        public DelegateCommand ChangePasswordCommand { get; }
        public DelegateCommand AccountManagementCommand { get; }
        public DelegateCommand AuditViewCommand { get; }
        public DelegateCommand EmergencyStopCommand { get; }
        public DelegateCommand ResetEmergencyCommand { get; }
        public DelegateCommand ExitCommand { get; }

        // ───── 用户 ─────
        private void OnSwitchUser()
        {
            // 取消登录框：保持原用户；强制改密被放弃：会话已被清空，没有合法用户，只能退出
            if (!_login.Run() && _identity.Current == null) ShellExit.Run(_container);
            Refresh();
        }

        // ───── 软件急停：停止所有运动和任务。不能替代硬件急停 ─────
        private void OnEmergencyStop()
        {
            _audit.Record(AuditCategory.Operation, "EmergencyStop", "software", detail: "界面急停按钮");
            _events.GetEvent<EmergencyStopEvent>().Publish(new EmergencyStopMessage { IsActive = true, Reason = "界面急停按钮" });
        }

        private void OnResetEmergency()
        {
            if (_user.Role < AccessRole.Engineer)
            {
                _audit.Record(AuditCategory.Operation, "ResetEmergency", "software", result: AuditResult.Denied, detail: "需要 Engineer 及以上角色");
                MessageBox.Show("解除急停需要工程师及以上权限。", "权限不足");
                return;
            }
            _audit.Record(AuditCategory.Operation, "ResetEmergency", "software");
            _events.GetEvent<EmergencyStopEvent>().Publish(new EmergencyStopMessage { IsActive = false, Reason = "手动解除" });
        }

        private void OnEmergencyEvent(EmergencyStopMessage m)
        {
            if (m == null) return;
            _emergency = m.IsActive; _emergencyText = m.IsActive ? (m.Reason ?? "") : "";
            RaisePropertyChanged(nameof(IsEmergency)); RaisePropertyChanged(nameof(EmergencyText));
            RaiseAll();
        }

        // ───── 退出（主窗口 Closing 调用）─────
        public bool ConfirmExit()
        {
            string msg = _runner.IsRunning ? "任务正在运行，退出将立即停止任务和运动。\n确定退出吗？" : "确定退出程序吗？";
            return MessageBox.Show(msg, "退出", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }

        public void Exit() { ShellExit.Run(_container); }

        // ───── 状态 ─────
        private void OnDeviceState(DeviceStateChangedEventArgs e)
        {
            DeviceStatusRow row = Devices.FirstOrDefault(d => string.Equals(d.Name, e.Name, StringComparison.OrdinalIgnoreCase));
            bool show = !Infra.Contains(e.Name) || e.State == DeviceState.Faulted;
            if (row == null) { if (!show) return; row = new DeviceStatusRow(e.Name); Devices.Add(row); }
            row.State = e.State; row.Error = e.Error;
            if (!show) Devices.Remove(row);
        }

        private void Refresh()
        {
            foreach (string n in new[] { nameof(WindowTitle), nameof(ProjectText), nameof(IdentityText) }) RaisePropertyChanged(n);
            RaiseAll();
        }

        private DelegateCommand Cmd(Action exec, Func<bool> can) { var c = new DelegateCommand(exec, can); _cmds.Add(c); return c; }
        private void RaiseAll() { foreach (DelegateCommand c in _cmds) c.RaiseCanExecuteChanged(); }

        private static string RoleName(AccessRole r)
        { return r == AccessRole.Administrator ? "管理员" : r == AccessRole.Engineer ? "工程师" : "操作员"; }
    }
}

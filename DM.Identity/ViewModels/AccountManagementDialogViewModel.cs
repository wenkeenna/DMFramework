using DM.Core.Identity;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DM.Identity.ViewModels
{
    public sealed class AccountManagementDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IAccountService _accounts;
        private AccountInfo _selected;
        private AccessRole _selectedRole, _newRole = AccessRole.Operator;
        private string _newUser = "", _newPass = "", _resetPass = "", _message = "";

        public AccountManagementDialogViewModel(IAccountService accounts)
        {
            _accounts = accounts;
            Roles = Enum.GetValues(typeof(AccessRole)).Cast<AccessRole>().ToArray();

            CreateCommand = new DelegateCommand(() => Run(() => _accounts.Create(NewUsername, NewPassword, NewRole),
                                                          () => { NewUsername = ""; NewPassword = ""; }));
            ResetCommand = new DelegateCommand(() => Run(() => _accounts.ResetPassword(Selected.Username, ResetPassword),
                                                         () => ResetPassword = ""), () => Selected != null).ObservesProperty(() => Selected);
            ApplyRoleCommand = new DelegateCommand(() => Run(() => _accounts.SetRole(Selected.Username, SelectedRole), null),
                                                   () => Selected != null).ObservesProperty(() => Selected);
            ToggleEnabledCommand = new DelegateCommand(() => Run(() => _accounts.SetEnabled(Selected.Username, !Selected.Enabled), null),
                                                       () => Selected != null).ObservesProperty(() => Selected);
            UnlockCommand = new DelegateCommand(() => Run(() => _accounts.Unlock(Selected.Username), null),
                                                () => Selected != null).ObservesProperty(() => Selected);
            DeleteCommand = new DelegateCommand(OnDelete, () => Selected != null).ObservesProperty(() => Selected);
            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK)));
        }

        public string Title => "账号管理";
        public event Action<IDialogResult> RequestClose;

        public ObservableCollection<AccountInfo> Accounts { get; } = new ObservableCollection<AccountInfo>();
        public AccessRole[] Roles { get; }

        public AccountInfo Selected
        {
            get { return _selected; }
            set { if (SetProperty(ref _selected, value) && value != null) SelectedRole = value.Role; }
        }
        public AccessRole SelectedRole { get { return _selectedRole; } set { SetProperty(ref _selectedRole, value); } }
        public string NewUsername { get { return _newUser; } set { SetProperty(ref _newUser, value ?? ""); } }
        public string NewPassword { get { return _newPass; } set { SetProperty(ref _newPass, value ?? ""); } }
        public AccessRole NewRole { get { return _newRole; } set { SetProperty(ref _newRole, value); } }
        public string ResetPassword { get { return _resetPass; } set { SetProperty(ref _resetPass, value ?? ""); } }
        public string Message { get { return _message; } private set { SetProperty(ref _message, value); } }

        public DelegateCommand CreateCommand { get; }
        public DelegateCommand ResetCommand { get; }
        public DelegateCommand ApplyRoleCommand { get; }
        public DelegateCommand ToggleEnabledCommand { get; }
        public DelegateCommand UnlockCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand CloseCommand { get; }

        private void Run(Func<IdentityResult> action, Action onSuccess)
        {
            IdentityResult r = action();
            Message = r.Ok ? "操作成功。" : r.Message;
            if (r.Ok && onSuccess != null) onSuccess();
            Refresh();
        }

        private void OnDelete()
        {
            if (MessageBox.Show("确定删除账号 " + Selected.Username + " 吗？", "确认", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            Run(() => _accounts.Delete(Selected.Username), null);
        }

        private void Refresh()
        {
            string keep = Selected == null ? null : Selected.Username;
            Accounts.Clear();
            foreach (AccountInfo a in _accounts.List()) Accounts.Add(a);
            Selected = keep == null ? null : Accounts.FirstOrDefault(a => a.Username == keep);
        }

        public bool CanCloseDialog() { return true; }
        public void OnDialogClosed() { NewPassword = ""; ResetPassword = ""; }
        public void OnDialogOpened(IDialogParameters p) { Refresh(); if (Accounts.Count == 0) Message = "需要管理员权限。"; }
    }
}

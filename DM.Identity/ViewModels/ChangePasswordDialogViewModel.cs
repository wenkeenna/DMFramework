using DM.Core.Identity;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.ViewModels
{
    /// <summary>参数 "Forced"=true 用于首次登录强制改密：取消按钮不可用；用户直接关窗口时宿主会退出程序。</summary>
    public sealed class ChangePasswordDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IIdentityService _identity;
        private string _old = "", _new = "", _confirm = "", _message = "";
        private bool _forced;

        public ChangePasswordDialogViewModel(IIdentityService identity)
        {
            _identity = identity;
            OkCommand = new DelegateCommand(OnOk);
            CancelCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)), () => !_forced);
        }

        public string Title => "修改密码";
        public event Action<IDialogResult> RequestClose;

        public string OldPassword { get { return _old; } set { SetProperty(ref _old, value ?? ""); } }
        public string NewPassword { get { return _new; } set { SetProperty(ref _new, value ?? ""); } }
        public string ConfirmPassword { get { return _confirm; } set { SetProperty(ref _confirm, value ?? ""); } }
        public string Message { get { return _message; } private set { SetProperty(ref _message, value); } }
        public bool Forced { get { return _forced; } private set { SetProperty(ref _forced, value); CancelCommand.RaiseCanExecuteChanged(); } }
        public DelegateCommand OkCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private void OnOk()
        {
            if (NewPassword != ConfirmPassword) { Message = "两次输入的新密码不一致。"; return; }
            IdentityResult r = _identity.ChangePassword(OldPassword, NewPassword);
            if (r.Ok) RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
            else Message = r.Message;
        }

        public bool CanCloseDialog() { return true; }
        public void OnDialogClosed() { OldPassword = NewPassword = ConfirmPassword = ""; }
        public void OnDialogOpened(IDialogParameters p) { Forced = p != null && p.ContainsKey("Forced") && p.GetValue<bool>("Forced"); }
    }
}

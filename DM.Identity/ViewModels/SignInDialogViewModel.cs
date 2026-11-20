using DM.Core.Identity;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DM.Identity.ViewModels
{
    public sealed class SignInDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IIdentityService _identity;
        private string _username = "", _password = "", _message = "";
        private bool _busy;
        private IReadOnlyList<string> _usernames = new string[0];
        private IAccountService _accounts;
        public SignInDialogViewModel(IIdentityService identity, IAccountService accounts)
        {
            _identity = identity;

            _accounts = accounts;

            SignInCommand = new DelegateCommand(async () => await SignInAsync(), () => !_busy && _username.Trim().Length > 0).ObservesProperty(() => Username);
         
            CancelCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));

          
        }

        public string Title => "用户登录";
        public event Action<IDialogResult> RequestClose;

        public string Username { get { return _username; } set { SetProperty(ref _username, value ?? ""); } }
        public string Password { get { return _password; } set { SetProperty(ref _password, value ?? ""); } }
        public string Message { get { return _message; } private set { SetProperty(ref _message, value); } }

        public IReadOnlyList<string> Usernames
        { get { return _usernames; } private set { SetProperty(ref _usernames, value); } }

        public DelegateCommand SignInCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand TestCommand { get; }
        private async Task SignInAsync()
        {
            _busy = true; 
            SignInCommand.RaiseCanExecuteChanged(); Message = "";
            try
            {
                string u = Username, p = Password;
                SignInResult r = await Task.Run(() => _identity.SignIn(u, p));      // 哈希计算放线程池，界面不卡
                if (r.Ok) RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
                else { Message = r.Message; Password = ""; }
            }
            catch (Exception ex) { Message = "登录出错：" + ex.Message; }             // async void 里不能漏异常，否则进程崩溃
            finally { _busy = false; SignInCommand.RaiseCanExecuteChanged(); }
        }

        public bool CanCloseDialog() { return true; }
        public void OnDialogClosed() { Password = ""; }
        public void OnDialogOpened(IDialogParameters parameters) 
        {
            try
            {
                Usernames = _identity.ListLoginNames();
                if (string.IsNullOrWhiteSpace(Username) && Usernames.Count > 0)
                    Username = Usernames[0];            // 默认选中第一个
            }
            catch
            {
                Usernames = new string[0];              // 列不出来不影响登录：仍然可以手输
            }
        }
    }
}

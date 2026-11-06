using DM.Core.Identity;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Shell
{
    /// <summary>
    /// 登录流程：登录 → （首次登录或被重置密码）强制改密。启动和"切换用户"共用。
    /// 返回 true 表示已有合法会话。强制改密被放弃时会登出（返回 false 且 Current 为 null），调用方据此决定退出程序。
    /// 对话框的回调在对话框关闭时同步执行，所以可以直接用局部变量收集结果。
    /// </summary>
    public sealed class LoginFlow
    {
        private readonly IDialogService _dialogs;
        private readonly IIdentityService _identity;

        public LoginFlow(IDialogService dialogs, IIdentityService identity) { _dialogs = dialogs; _identity = identity; }

        public bool Run()
        {
            ButtonResult res = ButtonResult.None;
            _dialogs.ShowDialog("SignInDialog", null, r => res = r.Result);
            if (res != ButtonResult.OK) return false;

            UserSession s = _identity.Current;
            if (s != null && s.MustChangePassword)
            {
                res = ButtonResult.None;
                _dialogs.ShowDialog("ChangePasswordDialog", new DialogParameters { { "Forced", true } }, r => res = r.Result);
                if (res != ButtonResult.OK) { _identity.SignOut(); return false; }
            }
            return true;
        }
    }
}

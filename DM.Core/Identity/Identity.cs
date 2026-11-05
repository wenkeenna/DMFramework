using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Core.Identity
{
    public enum AccessRole { Operator = 0, Engineer = 1, Administrator = 2 }    // 数值越大权限越高

    /// <summary>审计和动作网关只需要"当前是谁"。未登录返回 system 用户和最低角色，保证审计永远有主语。</summary>
    public interface ICurrentUser
    {
        string UserId { get; }
        string DisplayName { get; }
        AccessRole Role { get; }
        bool IsAuthenticated { get; }
    }

    public sealed class UserSession
    {
        public UserSession(string username, AccessRole role, DateTime signedInUtc, bool mustChangePassword)
        { Username = username; Role = role; SignedInUtc = signedInUtc; MustChangePassword = mustChangePassword; }
        public string Username { get; }
        public AccessRole Role { get; }
        public DateTime SignedInUtc { get; }
        public bool MustChangePassword { get; }
    }

    public class IdentityResult
    {
        public bool Ok { get; protected set; }
        public string Message { get; protected set; }
        public static IdentityResult Success() { return new IdentityResult { Ok = true }; }
        public static IdentityResult Fail(string message) { return new IdentityResult { Ok = false, Message = message }; }
    }

    public sealed class SignInResult : IdentityResult
    {
        public UserSession Session { get; private set; }
        public static SignInResult Success(UserSession s) { return new SignInResult { Ok = true, Session = s }; }
        public new static SignInResult Fail(string message) { return new SignInResult { Ok = false, Message = message }; }
    }

    public sealed class AccountInfo
    {
        public string Username { get; set; }
        public AccessRole Role { get; set; }
        public bool Enabled { get; set; }
        public bool IsLocked { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }

    public interface IIdentityService
    {
        UserSession Current { get; }                    // 未登录为 null
        event EventHandler SessionChanged;              // 在发起操作的线程触发，界面订阅时自行切回 UI 线程
        SignInResult SignIn(string username, string password);
        void SignOut();
        IdentityResult ChangePassword(string oldPassword, string newPassword);
    }

    /// <summary>所有方法内部都校验当前用户是管理员，否则返回失败并写 Denied 审计。</summary>
    public interface IAccountService
    {
        IReadOnlyList<AccountInfo> List();
        IdentityResult Create(string username, string password, AccessRole role);
        IdentityResult ResetPassword(string username, string newPassword);
        IdentityResult SetRole(string username, AccessRole role);
        IdentityResult SetEnabled(string username, bool enabled);
        IdentityResult Unlock(string username);
        IdentityResult Delete(string username);
    }
}

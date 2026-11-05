using DM.Core.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.Services
{
    /// <summary>
    /// 未登录时返回 system 用户和最低角色，保证审计永远有主语。
    /// 用 Lazy 是为了打断循环：AuditService → ICurrentUser → IdentityService → IAuditService。
    /// </summary>
    public sealed class CurrentUser : ICurrentUser
    {
        private readonly Lazy<IIdentityService> _svc;
        public CurrentUser(Lazy<IIdentityService> svc) { _svc = svc; }

        private UserSession S => _svc.Value.Current;

        public bool IsAuthenticated => S != null;
        public string UserId { get { UserSession s = S; return s == null ? "system" : s.Username; } }
        public string DisplayName => UserId;
        public AccessRole Role { get { UserSession s = S; return s == null ? AccessRole.Operator : s.Role; } }
    }
}

using DM.Core.Audit;
using DM.Core.Common;
using DM.Core.Identity;
using DM.Core.Logging;
using DM.Identity.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.Services
{
    /// <summary>
    /// 同时实现 IIdentityService 和 IAccountService，共用一把锁，账号状态的读改写不会交错。
    /// 事件在锁外触发。时间一律用 IClock（UTC），便于测试锁定逻辑。
    /// 重要：Current 是无锁读取（volatile）。审计服务在 Record 里会读 Current，而 Record 又常在持锁期间被调用，
    /// 如果 Current 要拿锁就会死锁。
    /// </summary>
    public sealed class IdentityService : IIdentityService, IAccountService
    {
        private const string Generic = "用户名或密码错误。";

        private readonly IAccountStore _store;
        private readonly IPasswordHasher _hasher;
        private readonly IClock _clock;
        private readonly IAuditService _audit;
        private readonly IAppLogger _log;
        private readonly IdentityOptions _opt;
        private readonly object _gate = new object();
        private volatile UserSession _session;
        private string _dummyHash;

        public IdentityService(IAccountStore store, IPasswordHasher hasher, IMachineConfig machine,
                               IClock clock, IAuditService audit, IAppLogger log)
        {
            _store = store; _hasher = hasher; _clock = clock; _audit = audit; _log = log;
            _opt = machine.Get<IdentityOptions>("Identity");
            if (_opt.MaxFailedAttempts < 1) _opt.MaxFailedAttempts = 1;
        }

        public UserSession Current => _session;
        public event EventHandler SessionChanged;

        /// <summary>首次运行创建管理员。必须在模块 Attach 里调用，不能放构造函数（会与审计服务循环依赖）。</summary>
        public void Initialize()
        {
            lock (_gate)
            {
                if (_store.Count() > 0) return;
                _store.Insert(new AccountRecord
                {
                    Username = _opt.BootstrapUsername,
                    PasswordHash = _hasher.Hash(_opt.BootstrapPassword),
                    Role = AccessRole.Administrator,
                    MustChangePassword = true,
                    CreatedUtc = _clock.UtcNow
                });
                _audit.Record(AuditCategory.Account, "BootstrapAdmin", _opt.BootstrapUsername, detail: "首次运行自动创建，首次登录必须修改密码");
                _log.Warn("已创建初始管理员账号 " + _opt.BootstrapUsername + "，首次登录需修改密码。");
            }
        }

        // ───────────── 登录 / 登出 ─────────────
        public SignInResult SignIn(string username, string password)
        {
            username = (username ?? "").Trim();
            SignInResult result;

            lock (_gate)
            {
                AccountRecord a = username.Length == 0 ? null : _store.Find(username);
                DateTime now = _clock.UtcNow;

                if (a == null)
                {
                    _hasher.Verify(password ?? "", DummyHash());            // 耗时与真实账号一致，防止探测账号是否存在
                    _audit.Record(AuditCategory.Account, "LoginFailed", username, result: AuditResult.Failed, detail: "账号不存在");
                    return SignInResult.Fail(Generic);
                }
                if (!a.Enabled)
                {
                    _audit.Record(AuditCategory.Account, "LoginDenied", a.Username, result: AuditResult.Denied, detail: "账号已停用");
                    return SignInResult.Fail("账号已停用，请联系管理员。");
                }
                if (a.LockedUntilUtc.HasValue && a.LockedUntilUtc.Value > now)
                {
                    int mins = (int)Math.Ceiling((a.LockedUntilUtc.Value - now).TotalMinutes);
                    _audit.Record(AuditCategory.Account, "LoginDenied", a.Username, result: AuditResult.Denied, detail: "账号锁定中");
                    return SignInResult.Fail("账号已锁定，请 " + mins + " 分钟后重试，或联系管理员解锁。");
                }

                if (!_hasher.Verify(password ?? "", a.PasswordHash))
                {
                    a.FailedCount++;
                    string detail = "密码错误（连续第 " + a.FailedCount + " 次）";
                    if (a.FailedCount >= _opt.MaxFailedAttempts)
                    {
                        a.LockedUntilUtc = now.AddMinutes(_opt.LockMinutes);
                        a.FailedCount = 0;
                        _audit.Record(AuditCategory.Account, "Locked", a.Username, result: AuditResult.Denied,
                                      detail: "连续失败 " + _opt.MaxFailedAttempts + " 次，锁定 " + _opt.LockMinutes + " 分钟");
                    }
                    _store.Update(a);
                    _audit.Record(AuditCategory.Account, "LoginFailed", a.Username, result: AuditResult.Failed, detail: detail);
                    return SignInResult.Fail(Generic);
                }

                if (_session != null)       // 不退出直接换人：先给上一个用户记一条登出
                    _audit.Record(AuditCategory.Account, "Logout", _session.Username, detail: "切换用户");

                a.FailedCount = 0; a.LockedUntilUtc = null; a.LastLoginUtc = now;
                _store.Update(a);
                _session = new UserSession(a.Username, a.Role, now, a.MustChangePassword);
                _audit.Record(AuditCategory.Account, "Login", a.Username, newValue: a.Role.ToString());
                result = SignInResult.Success(_session);
            }

            Raise();
            return result;
        }

        public void SignOut()
        {
            lock (_gate)
            {
                UserSession s = _session;
                if (s == null) return;
                _audit.Record(AuditCategory.Account, "Logout", s.Username);       // 先记录再清空，记录里的用户才是他本人
                _session = null;
            }
            Raise();
        }

        public IdentityResult ChangePassword(string oldPassword, string newPassword)
        {
            lock (_gate)
            {
                UserSession s = _session;
                if (s == null) return IdentityResult.Fail("未登录。");

                AccountRecord a = _store.Find(s.Username);
                if (a == null) return IdentityResult.Fail("账号不存在。");

                if (!_hasher.Verify(oldPassword ?? "", a.PasswordHash))
                {
                    _audit.Record(AuditCategory.Account, "ChangePassword", a.Username, result: AuditResult.Failed, detail: "原密码错误");
                    return IdentityResult.Fail("原密码错误。");
                }
                string err = CheckPassword(a.Username, newPassword);
                if (err == null && newPassword == oldPassword) err = "新密码不能与原密码相同。";
                if (err != null) return IdentityResult.Fail(err);

                a.PasswordHash = _hasher.Hash(newPassword);
                a.MustChangePassword = false;
                _store.Update(a);
                _session = new UserSession(a.Username, a.Role, s.SignedInUtc, false);
                _audit.Record(AuditCategory.Account, "ChangePassword", a.Username);      // 绝不记录密码内容
            }
            Raise();
            return IdentityResult.Success();
        }

        // ───────────── 账号管理（仅管理员）─────────────
        public IReadOnlyList<AccountInfo> List()
        {
            lock (_gate)
            {
                if (!IsAdmin()) return new AccountInfo[0];
                DateTime now = _clock.UtcNow;
                return _store.List().Select(a => new AccountInfo
                {
                    Username = a.Username,
                    Role = a.Role,
                    Enabled = a.Enabled,
                    IsLocked = a.LockedUntilUtc.HasValue && a.LockedUntilUtc.Value > now,
                    CreatedUtc = a.CreatedUtc,
                    LastLoginUtc = a.LastLoginUtc
                }).ToList();
            }
        }

        public IdentityResult Create(string username, string password, AccessRole role)
        {
            username = (username ?? "").Trim();
            return Admin("CreateAccount", username, () =>
            {
                if (username.Length == 0 || username.Length > 32 || username.Any(char.IsWhiteSpace))
                    return IdentityResult.Fail("用户名不能为空、不能含空白字符，且不超过 32 个字符。");
                if (_store.Find(username) != null) return IdentityResult.Fail("用户名已存在。");
                string err = CheckPassword(username, password);
                if (err != null) return IdentityResult.Fail(err);

                _store.Insert(new AccountRecord
                { Username = username, PasswordHash = _hasher.Hash(password), Role = role, MustChangePassword = true, CreatedUtc = _clock.UtcNow });
                _audit.Record(AuditCategory.Account, "CreateAccount", username, newValue: role.ToString());
                return IdentityResult.Success();
            });
        }

        public IdentityResult ResetPassword(string username, string newPassword)
        {
            return Admin("ResetPassword", username, () =>
            {
                AccountRecord a = _store.Find(username);
                if (a == null) return IdentityResult.Fail("账号不存在。");
                string err = CheckPassword(a.Username, newPassword);
                if (err != null) return IdentityResult.Fail(err);

                a.PasswordHash = _hasher.Hash(newPassword);
                a.MustChangePassword = true; a.FailedCount = 0; a.LockedUntilUtc = null;
                _store.Update(a);
                _audit.Record(AuditCategory.Account, "ResetPassword", a.Username);
                return IdentityResult.Success();
            });
        }

        public IdentityResult SetRole(string username, AccessRole role)
        {
            return Admin("SetRole", username, () =>
            {
                AccountRecord a = _store.Find(username);
                if (a == null) return IdentityResult.Fail("账号不存在。");
                if (a.Role == role) return IdentityResult.Success();
                if (role != AccessRole.Administrator && IsLastAdmin(a)) return IdentityResult.Fail("不能降低最后一个管理员的权限。");

                string old = a.Role.ToString();
                a.Role = role; _store.Update(a);
                _audit.Record(AuditCategory.Account, "SetRole", a.Username, old, role.ToString());
                return IdentityResult.Success();
            });
        }

        public IdentityResult SetEnabled(string username, bool enabled)
        {
            return Admin("SetEnabled", username, () =>
            {
                AccountRecord a = _store.Find(username);
                if (a == null) return IdentityResult.Fail("账号不存在。");
                if (!enabled && IsSelf(a)) return IdentityResult.Fail("不能停用当前登录的账号。");
                if (!enabled && IsLastAdmin(a)) return IdentityResult.Fail("不能停用最后一个管理员。");

                a.Enabled = enabled; _store.Update(a);
                _audit.Record(AuditCategory.Account, "SetEnabled", a.Username, (!enabled).ToString(), enabled.ToString());
                return IdentityResult.Success();
            });
        }

        public IdentityResult Unlock(string username)
        {
            return Admin("Unlock", username, () =>
            {
                AccountRecord a = _store.Find(username);
                if (a == null) return IdentityResult.Fail("账号不存在。");
                a.FailedCount = 0; a.LockedUntilUtc = null; _store.Update(a);
                _audit.Record(AuditCategory.Account, "Unlock", a.Username);
                return IdentityResult.Success();
            });
        }

        public IdentityResult Delete(string username)
        {
            return Admin("DeleteAccount", username, () =>
            {
                AccountRecord a = _store.Find(username);
                if (a == null) return IdentityResult.Fail("账号不存在。");
                if (IsSelf(a)) return IdentityResult.Fail("不能删除当前登录的账号。");
                if (IsLastAdmin(a)) return IdentityResult.Fail("不能删除最后一个管理员。");

                _store.Delete(a.Username);
                _audit.Record(AuditCategory.Account, "DeleteAccount", a.Username, oldValue: a.Role.ToString());
                return IdentityResult.Success();
            });
        }

        // ───────────── 内部 ─────────────
        private IdentityResult Admin(string action, string target, Func<IdentityResult> body)
        {
            lock (_gate)
            {
                if (!IsAdmin())
                {
                    _audit.Record(AuditCategory.Account, action, target, result: AuditResult.Denied, detail: "需要管理员权限");
                    return IdentityResult.Fail("需要管理员权限。");
                }
                try { return body(); }
                catch (Exception ex)
                {
                    _log.Error("账号操作失败：" + action, ex);
                    _audit.Record(AuditCategory.Account, action, target, result: AuditResult.Failed, detail: ex.Message);
                    return IdentityResult.Fail("操作失败：" + ex.Message);
                }
            }
        }

        private bool IsAdmin() { UserSession s = _session; return s != null && s.Role == AccessRole.Administrator; }
        private bool IsSelf(AccountRecord a) { UserSession s = _session; return s != null && string.Equals(s.Username, a.Username, StringComparison.OrdinalIgnoreCase); }
        private bool IsLastAdmin(AccountRecord a) { return a.Enabled && a.Role == AccessRole.Administrator && _store.CountEnabledAdmins() <= 1; }
        private string DummyHash() { return _dummyHash ?? (_dummyHash = _hasher.Hash("dummy-password")); }

        private string CheckPassword(string username, string pw)
        {
            if (string.IsNullOrEmpty(pw) || pw.Length < _opt.MinPasswordLength) return "密码至少 " + _opt.MinPasswordLength + " 位。";
            if (string.Equals(pw, username, StringComparison.OrdinalIgnoreCase)) return "密码不能与用户名相同。";
            return null;
        }

        private void Raise() { var h = SessionChanged; if (h != null) h(this, EventArgs.Empty); }
    }
}

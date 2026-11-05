using DM.Core.Actions;
using DM.Core.Audit;
using DM.Core.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.Actions
{
    public sealed class ActionGateway : IActionGateway
    {
        private readonly ISafetyState _safety;
        private readonly ICurrentUser _user;
        private readonly IAuditService _audit;

        public ActionGateway(ISafetyState safety, ICurrentUser user, IAuditService audit)
        { _safety = safety; _user = user; _audit = audit; }

        public Task RunAutoAsync(string action, string target, Func<Task> body)
        {
            CheckInterlock();
            return body();
        }

        public async Task RunManualAsync(string action, string target, AccessRole required, Func<Task> body, string detail = null)
        {
            if (_user.Role < required)
            {
                _audit.Record(AuditCategory.Operation, action, target, result: AuditResult.Denied, detail: "权限不足，需要 " + required);
                throw new UnauthorizedAccessException("权限不足：" + action + " 需要 " + required + " 及以上角色。");
            }
            try { CheckInterlock(); }
            catch (Exception ex)
            {
                _audit.Record(AuditCategory.Operation, action, target, result: AuditResult.Denied, detail: ex.Message);
                throw;
            }
            try
            {
                await body();
                _audit.Record(AuditCategory.Operation, action, target, newValue: detail);
            }
            catch (OperationCanceledException)
            {
                _audit.Record(AuditCategory.Operation, action, target, newValue: detail, result: AuditResult.Failed, detail: "已取消");
                throw;
            }
            catch (Exception ex)
            {
                _audit.Record(AuditCategory.Operation, action, target, newValue: detail, result: AuditResult.Failed, detail: ex.Message);
                throw;
            }
        }

        private void CheckInterlock()
        { if (_safety.IsEmergencyStopped) throw new InvalidOperationException("急停状态，禁止动作：" + _safety.Reason); }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Core.Audit
{
    /// <summary>只增不改：数值会存进库。</summary>
    public enum AuditCategory
    {
        System = 0, Account = 1, Project = 2, Parameter = 3, TaskList = 4,
        Operation = 5, Run = 6, License = 7, Alarm = 8
    }

    public enum AuditResult { Success = 0, Failed = 1, Denied = 2 }

    public sealed class AuditEntry
    {
        public long Id { get; set; }
        public DateTime TimeUtc { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
        public string Machine { get; set; }
        public string ProjectName { get; set; }
        public AuditCategory Category { get; set; }
        public string Action { get; set; }
        public string Target { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public AuditResult Result { get; set; }
        public string Detail { get; set; }
        public string TraceId { get; set; }
        public string PrevHash { get; set; }
        public string Hash { get; set; }
    }

    /// <summary>
    /// Record 永不抛异常、不阻塞调用线程；用户、机器、项目、时间由实现补全。
    /// 密码等敏感内容任何时候都不能放进参数里。
    /// </summary>
    public interface IAuditService
    {
        void Record(AuditCategory category, string action, string target,
                    string oldValue = null, string newValue = null,
                    AuditResult result = AuditResult.Success, string detail = null);

        /// <summary>之后的记录带同一个 TraceId；Dispose 结束。可能返回 null，using 对 null 安全。</summary>
        IDisposable BeginTrace(string traceId);
    }

    public sealed class NullAuditService : IAuditService
    {
        public static readonly NullAuditService Instance = new NullAuditService();
        public void Record(AuditCategory c, string a, string t, string o = null, string n = null,
                           AuditResult r = AuditResult.Success, string d = null)
        { }
        public IDisposable BeginTrace(string traceId) { return null; }
    }

    public sealed class AuditQuery
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public AuditCategory? Category { get; set; }
        public string User { get; set; }
        public string Text { get; set; }            // 在对象、动作、旧值、新值、说明里模糊匹配
        public int Limit { get; set; } = 500;
    }

    public sealed class AuditVerifyResult
    {
        public bool Ok { get; set; }
        public long Checked { get; set; }
        public long BrokenAtId { get; set; }
        public string Message { get; set; }
    }

    public interface IAuditReader
    {
        IReadOnlyList<AuditEntry> Query(AuditQuery query);
        AuditVerifyResult Verify();                 // 数据量大时较慢，界面上放按钮手动触发
    }
}

using DM.Core.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DM.Audit.Services
{
    /// <summary>写入和校验共用同一个哈希函数，保证一致。分隔符用不可见控制字符，避免内容里含 "|" 造成歧义。TimeUtc 必须是 UTC。</summary>
    public static class AuditHasher
    {
        public static string Compute(AuditEntry e)
        {
            string s = string.Join("\u001f",
                e.PrevHash ?? "", e.TimeUtc.ToString("o"), e.UserId, e.UserName, e.Role, e.Machine, e.ProjectName,
                (int)e.Category, e.Action, e.Target, e.OldValue, e.NewValue, (int)e.Result, e.Detail, e.TraceId);
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }
    }
}

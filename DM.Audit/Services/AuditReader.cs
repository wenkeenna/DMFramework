using DM.Core.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Audit.Services
{
    public sealed class AuditReader : IAuditReader
    {
        private readonly IAuditStore _store;
        public AuditReader(IAuditStore store) { _store = store; }

        public IReadOnlyList<AuditEntry> Query(AuditQuery q) { return _store.Query(q ?? new AuditQuery()); }

        /// <summary>按 id 顺序逐条重算哈希，并核对每条的 prev_hash 是否等于上一条的 hash。</summary>
        public AuditVerifyResult Verify()
        {
            string prev = "";
            long lastId = 0, count = 0;

            while (true)
            {
                IReadOnlyList<AuditEntry> batch = _store.ReadBatch(lastId, 2000);
                if (batch.Count == 0) break;

                foreach (AuditEntry e in batch)
                {
                    if ((e.PrevHash ?? "") != prev) return Broken(e.Id, count, "链接断裂：上一条哈希不匹配，可能有记录被删除。");
                    if (AuditHasher.Compute(e) != e.Hash) return Broken(e.Id, count, "内容与哈希不符，可能被篡改。");
                    prev = e.Hash; lastId = e.Id; count++;
                }
            }
            return new AuditVerifyResult { Ok = true, Checked = count, Message = "校验通过，共 " + count + " 条。" };
        }

        private static AuditVerifyResult Broken(long id, long count, string msg)
        { return new AuditVerifyResult { Ok = false, Checked = count, BrokenAtId = id, Message = "第 " + id + " 条：" + msg }; }
    }
}

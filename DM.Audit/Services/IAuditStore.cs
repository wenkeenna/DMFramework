using DM.Core.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Audit.Services
{
    public interface IAuditStore
    {
        void Append(IReadOnlyList<AuditEntry> entries);                 // 一个事务，Hash 已算好
        string GetLastHash();
        IReadOnlyList<AuditEntry> Query(AuditQuery q);
        IReadOnlyList<AuditEntry> ReadBatch(long afterId, int count);   // 按 id 升序，校验用
    }
}

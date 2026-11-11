using DM.Core.Audit;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Audit.Services
{
    /// <summary>
    /// WAL 模式（写入时界面可同时查询）；触发器禁止 UPDATE/DELETE，直接改库必须先删触发器，会被哈希链校验发现。
    /// 时间以 ISO 8601 "o" 字符串存储，同为 UTC 时字符串比较等价于时间比较。
    /// 如果你的 Microsoft.Data.Sqlite 是 6.0 及以上，把静态构造里改成 SQLitePCL.Batteries_V2.Init()。
    /// </summary>
    public sealed class SqliteAuditStore : IAuditStore
    {
        private const string Cols = "id,time_utc,user_id,user_name,role,machine,project,category,action,target,old_value,new_value,result,detail,trace_id,prev_hash,hash";
        private readonly string _cs;

        static SqliteAuditStore() 
        { 
            SQLitePCL.Batteries.Init(); 
        }

        public SqliteAuditStore(string dbPath)
        {
            _cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"
PRAGMA journal_mode=WAL;
CREATE TABLE IF NOT EXISTS audit(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  time_utc TEXT NOT NULL, user_id TEXT, user_name TEXT, role TEXT, machine TEXT, project TEXT,
  category INTEGER NOT NULL, action TEXT NOT NULL, target TEXT, old_value TEXT, new_value TEXT,
  result INTEGER NOT NULL, detail TEXT, trace_id TEXT, prev_hash TEXT, hash TEXT);
CREATE INDEX IF NOT EXISTS ix_audit_time ON audit(time_utc);
CREATE TRIGGER IF NOT EXISTS trg_audit_no_update BEFORE UPDATE ON audit
  BEGIN SELECT RAISE(ABORT,'audit is append-only'); END;
CREATE TRIGGER IF NOT EXISTS trg_audit_no_delete BEFORE DELETE ON audit
  BEGIN SELECT RAISE(ABORT,'audit is append-only'); END;";
                cmd.ExecuteNonQuery();
            }
        }

        private SqliteConnection Open() { var c = new SqliteConnection(_cs); c.Open(); return c; }

        public void Append(IReadOnlyList<AuditEntry> entries)
        {
            using (var c = Open())
            using (var tx = c.BeginTransaction())
            {
                foreach (AuditEntry e in entries)
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO audit(" + Cols.Substring(3) + @")
VALUES($t,$uid,$un,$role,$m,$p,$cat,$act,$tg,$ov,$nv,$res,$d,$tr,$ph,$h)";
                        P(cmd, "$t", e.TimeUtc.ToString("o")); P(cmd, "$uid", e.UserId); P(cmd, "$un", e.UserName);
                        P(cmd, "$role", e.Role); P(cmd, "$m", e.Machine); P(cmd, "$p", e.ProjectName);
                        P(cmd, "$cat", (int)e.Category); P(cmd, "$act", e.Action); P(cmd, "$tg", e.Target);
                        P(cmd, "$ov", e.OldValue); P(cmd, "$nv", e.NewValue); P(cmd, "$res", (int)e.Result);
                        P(cmd, "$d", e.Detail); P(cmd, "$tr", e.TraceId); P(cmd, "$ph", e.PrevHash ?? ""); P(cmd, "$h", e.Hash);
                        cmd.ExecuteNonQuery();
                    }
                tx.Commit();
            }
        }

        public string GetLastHash()
        {
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT hash FROM audit ORDER BY id DESC LIMIT 1";
                object o = cmd.ExecuteScalar();
                return o == null || o == DBNull.Value ? "" : (string)o;
            }
        }

        public IReadOnlyList<AuditEntry> Query(AuditQuery q)
        {
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT " + Cols + @" FROM audit WHERE
 ($f IS NULL OR time_utc>=$f) AND ($t IS NULL OR time_utc<=$t)
 AND ($cat IS NULL OR category=$cat) AND ($u IS NULL OR user_name=$u)
 AND ($txt IS NULL OR target LIKE $txt OR action LIKE $txt OR detail LIKE $txt OR old_value LIKE $txt OR new_value LIKE $txt)
ORDER BY id DESC LIMIT $lim";
                P(cmd, "$f", q.FromUtc.HasValue ? q.FromUtc.Value.ToString("o") : null);
                P(cmd, "$t", q.ToUtc.HasValue ? q.ToUtc.Value.ToString("o") : null);
                P(cmd, "$cat", q.Category.HasValue ? (object)(int)q.Category.Value : null);
                P(cmd, "$u", string.IsNullOrWhiteSpace(q.User) ? null : q.User);
                P(cmd, "$txt", string.IsNullOrWhiteSpace(q.Text) ? null : "%" + q.Text.Trim() + "%");
                P(cmd, "$lim", q.Limit <= 0 ? 500 : q.Limit);
                return ReadAll(cmd);
            }
        }

        public IReadOnlyList<AuditEntry> ReadBatch(long afterId, int count)
        {
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT " + Cols + " FROM audit WHERE id>$a ORDER BY id ASC LIMIT $n";
                P(cmd, "$a", afterId); P(cmd, "$n", count);
                return ReadAll(cmd);
            }
        }

        private static List<AuditEntry> ReadAll(SqliteCommand cmd)
        {
            var list = new List<AuditEntry>();
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    list.Add(new AuditEntry
                    {
                        Id = r.GetInt64(0),
                        TimeUtc = DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                        UserId = S(r, 2),
                        UserName = S(r, 3),
                        Role = S(r, 4),
                        Machine = S(r, 5),
                        ProjectName = S(r, 6),
                        Category = (AuditCategory)r.GetInt32(7),
                        Action = S(r, 8),
                        Target = S(r, 9),
                        OldValue = S(r, 10),
                        NewValue = S(r, 11),
                        Result = (AuditResult)r.GetInt32(12),
                        Detail = S(r, 13),
                        TraceId = S(r, 14),
                        PrevHash = S(r, 15),
                        Hash = S(r, 16)
                    });
            return list;
        }

        private static void P(SqliteCommand c, string n, object v) { c.Parameters.AddWithValue(n, v ?? DBNull.Value); }
        private static string S(SqliteDataReader r, int i) { return r.IsDBNull(i) ? null : r.GetString(i); }
    }
}

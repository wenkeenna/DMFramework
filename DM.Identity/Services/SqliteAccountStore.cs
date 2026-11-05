using DM.Core.Identity;
using DM.Identity.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Identity.Services
{
    /// <summary>用户名不区分大小写（COLLATE NOCASE）。每次操作新建连接，连接池由 SQLite 处理。</summary>
    public sealed class SqliteAccountStore : IAccountStore
    {
        private const string Cols = "username,password_hash,role,enabled,must_change,failed_count,locked_until,created_utc,last_login";
        private readonly string _cs;

        static SqliteAccountStore() { SQLitePCL.Batteries.Init(); }       // 与 Audit 相同，重复调用无害

        public SqliteAccountStore(string dbPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            _cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
            Exec(@"CREATE TABLE IF NOT EXISTS accounts(
  username TEXT PRIMARY KEY COLLATE NOCASE, password_hash TEXT NOT NULL, role INTEGER NOT NULL,
  enabled INTEGER NOT NULL, must_change INTEGER NOT NULL, failed_count INTEGER NOT NULL,
  locked_until TEXT, created_utc TEXT NOT NULL, last_login TEXT)", null);
        }

        public AccountRecord Find(string username)
        {
            var l = Query("SELECT " + Cols + " FROM accounts WHERE username=$u", c => P(c, "$u", username));
            return l.Count == 0 ? null : l[0];
        }

        public IReadOnlyList<AccountRecord> List() { return Query("SELECT " + Cols + " FROM accounts ORDER BY username", null); }
        public int Count() { return Scalar("SELECT COUNT(*) FROM accounts"); }
        public int CountEnabledAdmins() { return Scalar("SELECT COUNT(*) FROM accounts WHERE enabled=1 AND role=" + (int)AccessRole.Administrator); }

        public void Insert(AccountRecord a) { Exec("INSERT INTO accounts(" + Cols + ") VALUES($u,$h,$r,$e,$m,$f,$l,$c,$ll)", c => Fill(c, a)); }

        public void Update(AccountRecord a)
        {
            Exec(@"UPDATE accounts SET password_hash=$h,role=$r,enabled=$e,must_change=$m,failed_count=$f,
locked_until=$l,created_utc=$c,last_login=$ll WHERE username=$u", c => Fill(c, a));
        }

        public bool Delete(string username)
        {
            using (var cn = Open())
            using (var c = cn.CreateCommand())
            { c.CommandText = "DELETE FROM accounts WHERE username=$u"; P(c, "$u", username); return c.ExecuteNonQuery() > 0; }
        }

        private static void Fill(SqliteCommand c, AccountRecord a)
        {
            P(c, "$u", a.Username); P(c, "$h", a.PasswordHash); P(c, "$r", (int)a.Role);
            P(c, "$e", a.Enabled ? 1 : 0); P(c, "$m", a.MustChangePassword ? 1 : 0); P(c, "$f", a.FailedCount);
            P(c, "$l", a.LockedUntilUtc.HasValue ? a.LockedUntilUtc.Value.ToString("o") : null);
            P(c, "$c", a.CreatedUtc.ToString("o"));
            P(c, "$ll", a.LastLoginUtc.HasValue ? a.LastLoginUtc.Value.ToString("o") : null);
        }

        private List<AccountRecord> Query(string sql, Action<SqliteCommand> bind)
        {
            var list = new List<AccountRecord>();
            using (var cn = Open())
            using (var c = cn.CreateCommand())
            {
                c.CommandText = sql; if (bind != null) bind(c);
                using (var r = c.ExecuteReader())
                    while (r.Read())
                        list.Add(new AccountRecord
                        {
                            Username = r.GetString(0),
                            PasswordHash = r.GetString(1),
                            Role = (AccessRole)r.GetInt32(2),
                            Enabled = r.GetInt32(3) == 1,
                            MustChangePassword = r.GetInt32(4) == 1,
                            FailedCount = r.GetInt32(5),
                            LockedUntilUtc = D(r, 6),
                            CreatedUtc = D(r, 7).GetValueOrDefault(),
                            LastLoginUtc = D(r, 8)
                        });
            }
            return list;
        }

        private int Scalar(string sql)
        { using (var cn = Open()) using (var c = cn.CreateCommand()) { c.CommandText = sql; return Convert.ToInt32(c.ExecuteScalar()); } }

        private void Exec(string sql, Action<SqliteCommand> bind)
        { using (var cn = Open()) using (var c = cn.CreateCommand()) { c.CommandText = sql; if (bind != null) bind(c); c.ExecuteNonQuery(); } }

        private SqliteConnection Open() { var c = new SqliteConnection(_cs); c.Open(); return c; }
        private static void P(SqliteCommand c, string n, object v) { c.Parameters.AddWithValue(n, v ?? DBNull.Value); }
        private static DateTime? D(SqliteDataReader r, int i)
        { return r.IsDBNull(i) ? (DateTime?)null : DateTime.Parse(r.GetString(i), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind); }
    }
}

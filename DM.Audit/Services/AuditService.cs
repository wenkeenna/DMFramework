using DM.Core.Audit;
using DM.Core.Common;
using DM.Core.Devices;
using DM.Core.Identity;
using DM.Core.Logging;
using DM.Core.Projects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Audit.Services
{
    /// <summary>
    /// 1. Record 只补全字段并入队，微秒级返回；写库在单后台线程，批量事务。
    /// 2. 队列满（磁盘异常）时丢弃并计数，绝不阻塞设备运行。
    /// 3. 哈希链：写库成功后才推进 _lastHash，写库失败不会造成链断裂（本批丢失，后续记录仍可校验）。
    /// 4. TraceId 用 AsyncLocal，跨 await 自动传递：同一次运行的所有记录自动带同一个 ID。
    /// 5. 同时是 IDeviceSession：退出时记一条 Shutdown 并刷盘，复用设备中心的限时机制。
    /// 6. Lazy&lt;IProjectLifecycle&gt; 打断与项目生命周期的循环依赖（它自己也要记审计）。要求容器支持 Lazy&lt;T&gt;（DryIoc 支持）。
    /// </summary>
    public sealed class AuditService : IAuditService, IDeviceSession, IDisposable
    {
        private static readonly AsyncLocal<string> Trace = new AsyncLocal<string>();

        private readonly IAuditStore _store;
        private readonly ICurrentUser _user;
        private readonly Lazy<IProjectLifecycle> _project;
        private readonly IClock _clock;
        private readonly IAppLogger _log;
        private readonly BlockingCollection<AuditEntry> _queue = new BlockingCollection<AuditEntry>(10000);
        private readonly Thread _writer;
        private readonly string _machine = Environment.MachineName;
        private string _lastHash;
        private int _dropped;

        public AuditService(IAuditStore store, ICurrentUser user, Lazy<IProjectLifecycle> project, IClock clock, IAppLogger log)
        {
            _store = store; _user = user; _project = project; _clock = clock; _log = log;
            _lastHash = store.GetLastHash();
            _writer = new Thread(WriteLoop) { IsBackground = true, Name = "AuditWriter" };
            _writer.Start();
        }

        public void Record(AuditCategory category, string action, string target,
                           string oldValue = null, string newValue = null,
                           AuditResult result = AuditResult.Success, string detail = null)
        {
            if (_queue.IsAddingCompleted) return;                   // 已进入退出流程，TryAdd 会抛异常
            try
            {
                ProjectContext p = _project.Value.Current;
                var e = new AuditEntry
                {
                    TimeUtc = _clock.UtcNow,
                    UserId = _user.UserId,
                    UserName = _user.DisplayName,
                    Role = _user.Role.ToString(),
                    Machine = _machine,
                    ProjectName = p == null ? null : p.Name,
                    Category = category,
                    Action = action,
                    Target = target,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Result = result,
                    Detail = detail,
                    TraceId = Trace.Value
                };
                if (!_queue.TryAdd(e) && Interlocked.Increment(ref _dropped) % 100 == 1)
                    _log.Warn("审计队列已满，丢弃记录，累计 " + _dropped);
            }
            catch (Exception ex) { _log.Error("审计入队失败", ex); }       // 永不向外抛
        }

        public IDisposable BeginTrace(string traceId)
        {
            string old = Trace.Value;
            Trace.Value = traceId;
            return new Scope(() => Trace.Value = old);
        }

        private void WriteLoop()
        {
            var batch = new List<AuditEntry>(100);
            foreach (AuditEntry first in _queue.GetConsumingEnumerable())
            {
                batch.Add(first);
                AuditEntry next;
                while (batch.Count < 100 && _queue.TryTake(out next)) batch.Add(next);     // 攒一批
                Flush(batch);
                batch.Clear();
            }
        }

        private void Flush(List<AuditEntry> batch)
        {
            try
            {
                string h = _lastHash;
                foreach (AuditEntry e in batch) { e.PrevHash = h; e.Hash = AuditHasher.Compute(e); h = e.Hash; }
                _store.Append(batch);
                _lastHash = h;                                      // 成功后才推进
            }
            catch (Exception ex) { _log.Error("审计写库失败，本批 " + batch.Count + " 条丢失", ex); }
        }

        // ───── IDeviceSession：退出刷盘 ─────
        public string Name => "Audit";
        public Task ConnectAsync(CancellationToken ct) { return Task.CompletedTask; }

        public Task DisconnectAsync()
        {
            return Task.Run(() =>
            {
                Record(AuditCategory.System, "Shutdown", null);
                Dispose();
            });
        }

        public void Dispose()
        {
            if (_queue.IsAddingCompleted) return;
            _queue.CompleteAdding();
            _writer.Join(2000);                                     // 写线程把剩余的写完，最多等 2 秒
        }

        private sealed class Scope : IDisposable
        {
            private Action _a;
            public Scope(Action a) { _a = a; }
            public void Dispose() { Action a = Interlocked.Exchange(ref _a, null); if (a != null) a(); }
        }
    }
}

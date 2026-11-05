using DM.Core.Audit;
using DM.Core.Data;
using DM.Core.Events;
using DM.Core.Logging;
using DM.Core.Projects;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Engine.Projects
{
    /// <summary>
    /// 1. 打开/保存/关闭用信号量串行化，连点不会并发。
    /// 2. 按阶段分组执行，阶段内按登记顺序；关闭时只关"打开成功"的参与者，顺序整体反向。
    /// 3. 单个参与者失败只记录；Data 阶段失败则回滚并中止。
    /// 4. 打开新项目前先关闭旧项目（参与者各自决定是否保存）。
    /// </summary>
    public sealed class ProjectLifecycleCoordinator : IProjectLifecycle, IProjectAwareRegistry
    {
        private readonly List<IProjectAware> _participants = new List<IProjectAware>();
        private readonly object _gate = new object();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly IDataContainer _data;
        private readonly IEventAggregator _events;
        private readonly IAuditService _audit;
        private readonly IAppLogger _log;
        private List<IProjectAware> _active = new List<IProjectAware>();

        public ProjectLifecycleCoordinator(IDataContainer data, IEventAggregator events, IAuditService audit, IAppLogger log)
        { _data = data; _events = events; _audit = audit; _log = log; }

        public ProjectContext Current { get; private set; }

        public void Register(IProjectAware p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            lock (_gate) { if (!_participants.Contains(p)) _participants.Add(p); }
        }

        public async Task<ProjectOpenResult> OpenAsync(string name, string directory, CancellationToken ct = default(CancellationToken))
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (Current != null) await CloseCoreAsync(ct).ConfigureAwait(false);

                Directory.CreateDirectory(directory);
                var ctx = new ProjectContext(name, directory, _data);
                var opened = new List<IProjectAware>();
                var failures = new List<ProjectAwareFailure>();
                _log.Info("项目打开开始：" + name);

                foreach (ProjectPhase phase in Enum.GetValues(typeof(ProjectPhase)).Cast<ProjectPhase>().OrderBy(x => x))
                {
                    foreach (IProjectAware p in Snapshot(phase))
                    {
                        try { await p.OnOpenedAsync(ctx, ct).ConfigureAwait(false); opened.Add(p); }
                        catch (Exception ex)
                        {
                            failures.Add(new ProjectAwareFailure(p.Name, phase, ex));
                            _log.Error("项目参与者初始化失败：" + p.Name, ex);
                            _audit.Record(AuditCategory.Project, "OpenPart", p.Name, result: AuditResult.Failed, detail: ex.Message);

                            if (phase == ProjectPhase.Data)
                            {
                                await CloseListAsync(ctx, opened, ct).ConfigureAwait(false);   // 回滚已成功的
                                _audit.Record(AuditCategory.Project, "Open", name, result: AuditResult.Failed, detail: "数据加载失败：" + ex.Message);
                                return new ProjectOpenResult(true, failures);
                            }
                        }
                    }
                }

                _active = opened;
                Current = ctx;
                _audit.Record(AuditCategory.Project, "Open", name,
                    result: failures.Count == 0 ? AuditResult.Success : AuditResult.Failed,
                    detail: failures.Count == 0 ? null : "失败模块：" + string.Join(",", failures.Select(f => f.Name)));
                _events.GetEvent<ProjectOpenedEvent>().Publish(ctx);
                _log.Info("项目打开完成：" + name + "，失败模块数 " + failures.Count);
                return new ProjectOpenResult(false, failures);
            }
            finally { _lock.Release(); }
        }

        public async Task<IReadOnlyList<ProjectAwareFailure>> SaveAsync(CancellationToken ct = default(CancellationToken))
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var failures = new List<ProjectAwareFailure>();
                ProjectContext ctx = Current;
                if (ctx == null) return failures;

                foreach (IProjectAware p in _active)
                {
                    var s = p as IProjectSavable;
                    if (s == null) continue;
                    try { await s.OnSaveAsync(ctx, ct).ConfigureAwait(false); }
                    catch (Exception ex) { failures.Add(new ProjectAwareFailure(p.Name, p.Phase, ex)); _log.Error("项目保存失败：" + p.Name, ex); }
                }
                _audit.Record(AuditCategory.Project, "Save", ctx.Name,
                    result: failures.Count == 0 ? AuditResult.Success : AuditResult.Failed,
                    detail: failures.Count == 0 ? null : string.Join("；", failures.Select(f => f.Name + "：" + f.Error.Message)));
                return failures;
            }
            finally { _lock.Release(); }
        }

        public async Task CloseAsync(CancellationToken ct = default(CancellationToken))
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try { await CloseCoreAsync(ct).ConfigureAwait(false); }
            finally { _lock.Release(); }
        }

        private async Task CloseCoreAsync(CancellationToken ct)
        {
            ProjectContext ctx = Current;
            if (ctx == null) return;
            List<IProjectAware> list = _active;
            _active = new List<IProjectAware>();
            await CloseListAsync(ctx, list, ct).ConfigureAwait(false);

            Current = null;
            _audit.Record(AuditCategory.Project, "Close", ctx.Name);
            _events.GetEvent<ProjectClosedEvent>().Publish(ctx.Name);
            _log.Info("项目已关闭：" + ctx.Name);
        }

        private async Task CloseListAsync(ProjectContext ctx, List<IProjectAware> list, CancellationToken ct)
        {
            for (int i = list.Count - 1; i >= 0; i--)           // 打开顺序的反向
            {
                try { await list[i].OnClosingAsync(ctx, ct).ConfigureAwait(false); }
                catch (Exception ex) { _log.Error("项目参与者关闭失败：" + list[i].Name, ex); }
            }
        }

        private List<IProjectAware> Snapshot(ProjectPhase phase)
        { lock (_gate) return _participants.Where(x => x.Phase == phase).ToList(); }
    }
}

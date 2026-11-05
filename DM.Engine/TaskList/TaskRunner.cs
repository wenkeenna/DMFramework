using DM.Core.Audit;
using DM.Core.Events;
using DM.Core.Logging;
using DM.Core.TaskList;
using Prism.Events;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Engine.TaskList
{
    /// <summary>
    /// 1. 预检：运行前校验所有启用的任务，问题一次报全，且不执行任何动作。
    /// 2. 单例 + Interlocked：同一时刻只有一个运行。
    /// 3. 急停订阅在这里：不依赖某个界面存活，任意线程直接取消。
    /// 4. 不用 ConfigureAwait(false)：事件回到调用方线程（UI）。
    /// 5. 取消 → 令牌传给任务，任务（经服务）自己停下硬件。超时用链接令牌，区分"超时"与"用户取消"。
    /// </summary>
    public sealed class TaskRunner : ITaskRunner
    {
        private sealed class Planned { public TaskItem Item; public string Name; public ITaskType Type; }

        private readonly ITaskTypeRegistry _types;
        private readonly IContainerProvider _container;
        private readonly IEventAggregator _events;
        private readonly IAuditService _audit;
        private readonly IAppLogger _log;
        private int _running;
        private CancellationTokenSource _cts;
        private readonly object _gate = new object();

        public TaskRunner(ITaskTypeRegistry types, IContainerProvider container, IEventAggregator events,
                          IAuditService audit, IAppLogger log)
        {
            _types = types; _container = container; _events = events; _audit = audit; _log = log;
            events.GetEvent<EmergencyStopEvent>().Subscribe(
                m => { if (m != null && m.IsActive) Stop("急停：" + m.Reason); }, ThreadOption.PublisherThread);
        }

        public bool IsRunning => Volatile.Read(ref _running) == 1;
        public event EventHandler<TaskStartedEventArgs> TaskStarted;
        public event EventHandler<TaskFinishedEventArgs> TaskFinished;
        public event EventHandler RunningChanged;

        public async Task<RunReport> RunAsync(IReadOnlyList<TaskItem> tasks, bool loop)
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) throw new InvalidOperationException("已有任务在运行。");

            var cts = new CancellationTokenSource();
            lock (_gate) { _cts = cts; }
            var report = new RunReport();
            var total = Stopwatch.StartNew();
            RunningChanged?.Invoke(this, EventArgs.Empty);

            using (_audit.BeginTrace(Guid.NewGuid().ToString("N")))
            {
                _audit.Record(AuditCategory.Run, loop ? "LoopStart" : "Start", null, newValue: tasks.Count + " tasks");
                try
                {
                    List<Planned> plan = Prepare(tasks, report);
                    if (plan == null) return report;

                    do
                    {
                        RunReport once = await RunOnceAsync(plan, cts.Token);
                        if (once.Outcome != RunOutcome.Completed)
                        { report.Outcome = once.Outcome; report.Error = once.Error; report.FailedTaskId = once.FailedTaskId; return report; }
                        report.Cycles++;
                        if (loop) await Task.Delay(10, cts.Token);          // 让出时间片，也是一个停止响应点
                    } while (loop);

                    report.Outcome = RunOutcome.Completed;
                    return report;
                }
                catch (OperationCanceledException) { report.Outcome = RunOutcome.Cancelled; return report; }
                catch (Exception ex)
                {
                    _log.Error("任务运行异常", ex);
                    report.Outcome = RunOutcome.Failed; report.Error = ex.Message;
                    return report;
                }
                finally
                {
                    total.Stop(); report.Elapsed = total.Elapsed;
                    lock (_gate) { _cts = null; }
                    cts.Dispose();
                    Interlocked.Exchange(ref _running, 0);
                    RunningChanged?.Invoke(this, EventArgs.Empty);
                    _audit.Record(AuditCategory.Run, "End", null, newValue: report.Outcome.ToString(),
                        result: report.Outcome == RunOutcome.Failed ? AuditResult.Failed : AuditResult.Success, detail: report.Error);
                }
            }
        }

        public void Stop(string reason)
        {
            CancellationTokenSource cts;
            lock (_gate) { cts = _cts; }
            if (cts == null) return;
            _log.Info("请求停止任务：" + reason);
            _audit.Record(AuditCategory.Run, "Stop", null, detail: reason);
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
        }

        private List<Planned> Prepare(IReadOnlyList<TaskItem> tasks, RunReport report)
        {
            var plan = new List<Planned>();
            var errors = new List<string>();

            foreach (TaskItem t in tasks)
            {
                if (t == null || !t.Enabled) continue;
                string name = string.IsNullOrWhiteSpace(t.Name) ? t.Id : t.Name;

                ITaskType type;
                if (!_types.TryGet(t.TypeKey, out type))
                { errors.Add(name + "：未知任务类型 " + t.TypeKey + "（是否缺少扩展包？）"); continue; }

                string err;
                try { err = type.Validate(t.Data); } catch (Exception ex) { err = ex.Message; }
                if (err != null) { errors.Add(name + "：" + err); continue; }

                plan.Add(new Planned { Item = t, Name = name, Type = type });
            }

            if (errors.Count > 0)
            {
                report.Outcome = RunOutcome.Failed;
                report.Error = "运行前校验失败，未执行任何任务：\n" + string.Join("\n", errors);
                _audit.Record(AuditCategory.Run, "Rejected", null, result: AuditResult.Failed, detail: report.Error);
                return null;
            }
            return plan;
        }

        private async Task<RunReport> RunOnceAsync(List<Planned> plan, CancellationToken ct)
        {
            Guid runId = Guid.NewGuid();

            foreach (Planned p in plan)
            {
                ct.ThrowIfCancellationRequested();
                var ctx = new RunContext(_container, _events, runId, p.Name);
                var finished = new TaskFinishedEventArgs { TaskId = p.Item.Id };
                var sw = Stopwatch.StartNew();
                TaskStarted?.Invoke(this, new TaskStartedEventArgs { TaskId = p.Item.Id });

                try
                {
                    ctx.Log("开始执行");
                    using (var tcts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        if (p.Item.TimeoutMs > 0) tcts.CancelAfter(p.Item.TimeoutMs);
                        try { await p.Type.ExecuteAsync(p.Item.Data, ctx, tcts.Token); finished.Success = true; }
                        catch (OperationCanceledException)
                        {
                            if (ct.IsCancellationRequested) { finished.Cancelled = true; throw; }    // 用户停止/急停
                            finished.Error = "执行超时（" + p.Item.TimeoutMs + "ms）。";              // 任务超时
                        }
                        catch (Exception ex) { finished.Error = ex.Message; }
                    }

                    if (!finished.Success)
                    {
                        ctx.Log("执行失败：" + finished.Error);
                        return new RunReport { Outcome = RunOutcome.Failed, Error = finished.Error, FailedTaskId = p.Item.Id };
                    }
                    ctx.Log("执行完成");
                    if (p.Item.PauseAfter) return new RunReport { Outcome = RunOutcome.Paused };
                }
                finally
                {
                    sw.Stop(); finished.Elapsed = sw.Elapsed;
                    TaskFinished?.Invoke(this, finished);           // 成功、失败、暂停、取消都会触发，界面状态不会卡住
                    _audit.Record(AuditCategory.Run, "Task", p.Name, newValue: p.Item.TypeKey,
                        result: finished.Success ? AuditResult.Success : AuditResult.Failed,
                        detail: finished.Cancelled ? "已取消" : finished.Error);
                }
            }
            return new RunReport { Outcome = RunOutcome.Completed };
        }
    }
}

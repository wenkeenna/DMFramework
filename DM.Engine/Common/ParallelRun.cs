using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Engine.Common
{
    /// <summary>并行执行一组工作：任一失败立即取消其余，并抛出真实故障而不是被连带取消的那个。</summary>
    public static class ParallelRun
    {
        public static async Task AllAsync(IReadOnlyList<Func<CancellationToken, Task>> work, CancellationToken ct)
        {
            using (var group = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                List<Task> tasks = work.Select(w => Guard(w, group)).ToList();
                try { await Task.WhenAll(tasks); } catch { /* 根因从 tasks 里取 */ }

                Exception root = tasks.Where(t => t.IsFaulted).Select(t => t.Exception.GetBaseException())
                                      .FirstOrDefault(e => !(e is OperationCanceledException));
                if (root != null) ExceptionDispatchInfo.Capture(root).Throw();
                ct.ThrowIfCancellationRequested();
            }
        }

        private static async Task Guard(Func<CancellationToken, Task> w, CancellationTokenSource group)
        {
            try { await w(group.Token); }
            catch (OperationCanceledException) { throw; }
            catch { group.Cancel(); throw; }
        }
    }
}

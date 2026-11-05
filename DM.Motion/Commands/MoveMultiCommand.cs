using DM.Core.Actions;
using DM.Core.Common;
using DM.Scripting;
using DM.Motion.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DM.Core.Scripting;

namespace DM.Motion.Commands
{
    /// <summary>
    /// 多轴同时运动，一行完成。轴组格式：轴:位置[:速度];轴:位置[:速度]，如 "X:100:80;Y:200"。
    /// 任一轴失败 → 取消同组其他轴（MotionService 立即停轴），并抛出真实故障而非连带取消。
    /// 需要"先 X、Y 同时，再 Z"这样的分批，就写多行 MOVE_MULTI。
    /// </summary>
    public sealed class MoveMultiCommand : IScriptCommand
    {
        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "MOVE_MULTI",
            Group = "轴控制",
            Description = "多轴绝对运动，同时开始",
            Params = new[] { new ScriptParamSpec { Name = "轴组", Type = typeof(string), Description = "X:100:80;Y:200" } }
        };

        private sealed class Item { public string Axis; public double Pos; public double? Vel; }

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            List<Item> items = Parse(a.Get<string>("轴组"));
            var motion = ctx.Get<IMotionService>();
            return ctx.Get<IActionGateway>().RunAutoAsync("MOVE_MULTI", string.Join(",", items.Select(i => i.Axis)),
                () => RunAsync(motion, items, ct));
        }

        private static async Task RunAsync(IMotionService motion, List<Item> items, CancellationToken ct)
        {
            using (var group = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                List<Task> tasks = items.Select(i => Guarded(
                    () => motion.MoveAsync(i.Axis, i.Pos, false, i.Vel, group.Token), group)).ToList();

                try { await Task.WhenAll(tasks); } catch { /* 根因从 tasks 里取 */ }

                Exception root = tasks.Where(t => t.IsFaulted)
                                      .Select(t => t.Exception.GetBaseException())
                                      .FirstOrDefault(e => !(e is OperationCanceledException));
                if (root != null) ExceptionDispatchInfo.Capture(root).Throw();
                ct.ThrowIfCancellationRequested();
            }
        }

        private static async Task Guarded(Func<Task> f, CancellationTokenSource group)
        {
            try { await f(); }
            catch (OperationCanceledException) { throw; }
            catch { group.Cancel(); throw; }        // 本轴出错：让同组其他轴立刻停
        }

        private static List<Item> Parse(string text)
        {
            var list = new List<Item>();
            foreach (string part in (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = part.Split(':');
                double pos;
                if (f.Length < 2 || f.Length > 3 || string.IsNullOrWhiteSpace(f[0])
                    || !double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out pos))
                    throw new InvalidOperationException("轴组格式错误：" + part + "（应为 轴:位置[:速度]）");

                double? v = null;
                if (f.Length == 3)
                {
                    double vel;
                    if (!double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out vel) || vel <= 0)
                        throw new InvalidOperationException("轴组速度无效：" + part);
                    v = vel;
                }
                list.Add(new Item { Axis = f[0].Trim(), Pos = pos, Vel = v });
            }
            if (list.Count == 0) throw new InvalidOperationException("轴组不能为空。");
            var dup = list.GroupBy(i => i.Axis, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new InvalidOperationException("轴组内同一根轴出现多次：" + dup.Key);
            return list;
        }
    }
}

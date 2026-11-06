using DM.Core.Common;
using DM.Engine.Common;
using DM.Engine.TaskList;
using DM.Process.Models;
using DM.Process.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Process.Tasks
{
    public sealed class TouchProbeTaskType : JsonTaskType<TouchProbeData>
    {
        public const string TypeKey = "touch-probe";
        public TouchProbeTaskType() : base(TypeKey, "接触探测", "点胶工艺", "TouchProbeEditorView") { }

        private static List<ProbeLaneData> Enabled(TouchProbeData d)
        { return new[] { d.Left, d.Right }.Where(l => l != null && l.Enabled).ToList(); }

        protected override string Check(TouchProbeData d)
        {
            List<ProbeLaneData> lanes = Enabled(d);
            if (lanes.Count == 0) return "至少启用一个探测通道。";
            foreach (ProbeLaneData l in lanes)
            {
                string e = l.ToProbe().Check();
                if (e != null) return l.Name + "：" + e;
            }
            if (d.RunInParallel && lanes.Count == 2 && string.Equals(lanes[0].Axis.Trim(), lanes[1].Axis.Trim(), StringComparison.OrdinalIgnoreCase))
                return "并行探测时，两个通道不能使用同一根轴。";
            return null;
        }

        protected override async Task RunAsync(TouchProbeData d, IRunContext ctx, CancellationToken ct)
        {
            var probe = ctx.Get<IProbeService>();
            List<ProbeLaneData> lanes = Enabled(d);

            if (d.RunInParallel)
            {
                var work = lanes.Select(l =>
                {
                    ProbeParams p = l.ToProbe(); string name = l.Name;
                    return (Func<CancellationToken, Task>)(t => probe.ProbeAsync(p, m => ctx.Log("[" + name + "] " + m), t));
                }).ToList();
                await ParallelRun.AllAsync(work, ct);                // 一边失败，另一边立即停
            }
            else
                foreach (ProbeLaneData l in lanes)
                {
                    string name = l.Name;
                    await probe.ProbeAsync(l.ToProbe(), m => ctx.Log("[" + name + "] " + m), ct);
                }
        }

        protected override string Describe(TouchProbeData d)
        {
            var names = Enabled(d).Select(l => l.Name).ToList();
            return names.Count == 0 ? "（未启用通道）" : string.Join("、", names) + (d.RunInParallel && names.Count > 1 ? "（并行）" : "");
        }
    }
}

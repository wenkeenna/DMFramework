using DM.Core.Common;
using DM.Core.Data;
using DM.Core.Scripting;
using DM.Motion.Services;
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
    /// <summary>PROBE 轴名 力通道 POS|NEG [最大行程] [步距] [速度] [稳定ms] [触发力] [回退] [结果键]</summary>
    public sealed class ProbeCommand : IScriptCommand
    {
        private readonly IProbeService _probe;
        private readonly IDataContainer _data;

        public ProbeCommand(IMotionService motion, ForceSourceProvider force, IProbeService probe, IDataContainer data)
        {
            _probe = probe; _data = data;
            Spec = new ScriptCommandSpec
            {
                Name = "PROBE",
                Group = "点胶工艺",
                Description = "接触探测：逐步靠近直到力达到触发值，可把接触位置写入数据容器",
                Params = new[]
                {
                    new ScriptParamSpec { Name = "轴名", Type = typeof(string), Suggest = () => motion.Axes.Select(a => a.Name) },
                    new ScriptParamSpec { Name = "力通道", Type = typeof(string), Suggest = () => force.ChannelsOrEmpty() },
                    new ScriptParamSpec { Name = "方向", Type = typeof(string), Options = new[] { "POS", "NEG" } },
                    new ScriptParamSpec { Name = "最大行程", Type = typeof(double), Required = false, Default = 5.0, Positive = true },
                    new ScriptParamSpec { Name = "步距", Type = typeof(double), Required = false, Default = 0.05, Positive = true },
                    new ScriptParamSpec { Name = "速度", Type = typeof(double), Required = false, Default = 0.1, Positive = true },
                    new ScriptParamSpec { Name = "稳定ms", Type = typeof(int), Required = false, Default = 200 },
                    new ScriptParamSpec { Name = "触发力", Type = typeof(double), Required = false, Default = 0.5, Positive = true },
                    new ScriptParamSpec { Name = "回退", Type = typeof(double), Required = false, Default = 0.5, Description = "触发后沿反方向回退的距离" },
                    new ScriptParamSpec { Name = "结果键", Type = typeof(string), Required = false, Default = "", IsDataKey = true, Description = "如 Probe.Z，记录接触位置" }
                }
            };
        }

        public ScriptCommandSpec Spec { get; }

        public async Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            var p = new ProbeParams
            {
                Axis = a.Get<string>("轴名"),
                Channel = a.Get<string>("力通道"),
                Direction = a.Get<string>("方向").ToUpperInvariant() == "POS" ? ProbeDirection.Positive : ProbeDirection.Negative,
                MaxTravel = a.Get<double>("最大行程"),
                StepDistance = a.Get<double>("步距"),
                StepVelocity = a.Get<double>("速度"),
                SettleMs = a.Get<int>("稳定ms"),
                TriggerForce = a.Get<double>("触发力"),
                RetreatValue = a.Get<double>("回退")
            };
            double pos = await _probe.ProbeAsync(p, ctx.Log, ct);
            string key = a.Get<string>("结果键");
            if (!string.IsNullOrEmpty(key)) _data.Set(key, pos, DataScope.Runtime);
        }
    }

    /// <summary>GLUE_POINT X Y：按 machine.json "Glue" 段的工艺设置点一个点。坐标可写 @Root.Child。</summary>
    public sealed class GluePointCommand : IScriptCommand
    {
        private readonly IMachineConfig _machine;
        private readonly IGlueService _glue;
        private readonly IDataContainer _data;

        public GluePointCommand(IMachineConfig machine, IGlueService glue, IDataContainer data) { _machine = machine; _glue = glue; _data = data; }

        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "GLUE_POINT",
            Group = "点胶工艺",
            Description = "点胶（点）。工艺设置取自 machine.json 的 Glue 段",
            Params = new[]
            {
                new ScriptParamSpec { Name = "X", Type = typeof(double), AllowBinding = true },
                new ScriptParamSpec { Name = "Y", Type = typeof(double), AllowBinding = true }
            }
        };

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            var s = _machine.Get<GlueSettings>("Glue");
            GlueJob job = GlueJobs.Make(s, GlueMode.Point, GlueLineDirection.X, a.Get<double>("X"), a.Get<double>("Y"), 0, 0, GlueJobs.CalibZ(s, _data));
            return _glue.RunAsync(job, ctx.Log, ct);
        }
    }

    /// <summary>GLUE_LINE 起点X 起点Y 终点X 终点Y [X|Y]：沿 X 或 Y 方向划线，方向缺省为 X（X 方向只用终点 X，Y 方向只用终点 Y）。</summary>
    public sealed class GlueLineCommand : IScriptCommand
    {
        private readonly IMachineConfig _machine;
        private readonly IGlueService _glue;
        private readonly IDataContainer _data;

        public GlueLineCommand(IMachineConfig machine, IGlueService glue, IDataContainer data) { _machine = machine; _glue = glue; _data = data; }

        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "GLUE_LINE",
            Group = "点胶工艺",
            Description = "点胶（线）。工艺设置取自 machine.json 的 Glue 段",
            Params = new[]
            {
                new ScriptParamSpec { Name = "起点X", Type = typeof(double), AllowBinding = true },
                new ScriptParamSpec { Name = "起点Y", Type = typeof(double), AllowBinding = true },
                new ScriptParamSpec { Name = "终点X", Type = typeof(double), AllowBinding = true },
                new ScriptParamSpec { Name = "终点Y", Type = typeof(double), AllowBinding = true },
                new ScriptParamSpec { Name = "方向", Type = typeof(string), Required = false, Default = "X", Options = new[] { "X", "Y" } }
            }
        };

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            var s = _machine.Get<GlueSettings>("Glue");
            GlueLineDirection dir = a.Get<string>("方向").ToUpperInvariant() == "Y" ? GlueLineDirection.Y : GlueLineDirection.X;
            GlueJob job = GlueJobs.Make(s, GlueMode.Line, dir, a.Get<double>("起点X"), a.Get<double>("起点Y"),
                                        a.Get<double>("终点X"), a.Get<double>("终点Y"), GlueJobs.CalibZ(s, _data));
            return _glue.RunAsync(job, ctx.Log, ct);
        }
    }
}

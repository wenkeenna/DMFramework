using DM.Core.Actions;
using DM.Core.Common;
using DM.Core.Data;
using DM.Core.Scripting;
using DM.Engine.Common;
using DM.Motion.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Commands
{
    public abstract class MoveCommandBase : IScriptCommand
    {
        private readonly IMotionService _motion;
        private readonly IActionGateway _gate;
        private readonly bool _relative;

        protected MoveCommandBase(IMotionService motion, IActionGateway gate, string name, string description, bool relative)
        {
            _motion = motion; _gate = gate; _relative = relative;
            Spec = new ScriptCommandSpec
            {
                Name = name,
                Group = "轴控制",
                Description = description,
                Params = new[]
                {
                 new ScriptParamSpec { Name = "轴名", Type = typeof(string), Suggest = () => motion.Axes.Select(a => a.Name) },
                 new ScriptParamSpec { Name = "位置", Type = typeof(double), AllowBinding = true },
                 new ScriptParamSpec { Name = "速度", Type = typeof(double), Required = false, Default = 0.0, Positive = true, Description = "省略则用轴默认速度" }
             }
            };
        }

        public ScriptCommandSpec Spec { get; }

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            string axis = a.Get<string>("轴名");
            double pos = a.Get<double>("位置"), vel = a.Get<double>("速度");
            return _gate.RunAutoAsync(Spec.Name, axis, () => _motion.MoveAsync(axis, pos, _relative, vel > 0 ? vel : (double?)null, ct));
        }
    }

    public sealed class MoveAbsCommand : MoveCommandBase
    { public MoveAbsCommand(IMotionService m, IActionGateway g) : base(m, g, "MOVE_ABS", "绝对运动", false) { } }

    public sealed class MoveRelCommand : MoveCommandBase
    { public MoveRelCommand(IMotionService m, IActionGateway g) : base(m, g, "MOVE_REL", "相对运动", true) { } }

    public sealed class HomeCommand : IScriptCommand
    {
        private readonly IMotionService _motion;
        private readonly IActionGateway _gate;

        public HomeCommand(IMotionService motion, IActionGateway gate)
        {
            _motion = motion; _gate = gate;
            Spec = new ScriptCommandSpec
            {
                Name = "HOME",
                Group = "轴控制",
                Description = "轴回零",
                Params = new[] { new ScriptParamSpec { Name = "轴名", Type = typeof(string), Suggest = () => motion.Axes.Select(a => a.Name) } }
            };
        }

        public ScriptCommandSpec Spec { get; }

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        { string axis = a.Get<string>("轴名"); return _gate.RunAutoAsync("HOME", axis, () => _motion.HomeAsync(axis, ct)); }
    }

    /// <summary>两轴直线插补：LINE2 轴A 目标A 轴B 目标B 速度</summary>
    public sealed class Line2Command : IScriptCommand, IScriptCommandCheck
    {
        private readonly IMotionService _motion;
        private readonly IActionGateway _gate;

        public Line2Command(IMotionService motion, IActionGateway gate)
        {
            _motion = motion; _gate = gate;
            Func<IEnumerable<string>> axes = () => motion.Axes.Select(x => x.Name);
            Spec = new ScriptCommandSpec
            {
                Name = "LINE2",
                Group = "轴控制",
                Description = "两轴直线插补",
                Params = new[]
                {
                 new ScriptParamSpec { Name = "轴A", Type = typeof(string), Suggest = axes },
                 new ScriptParamSpec { Name = "目标A", Type = typeof(double), AllowBinding = true },
                 new ScriptParamSpec { Name = "轴B", Type = typeof(string), Suggest = axes },
                 new ScriptParamSpec { Name = "目标B", Type = typeof(double), AllowBinding = true },
                 new ScriptParamSpec { Name = "速度", Type = typeof(double), Positive = true, Description = "合成速度" }
             }
            };
        }

        public ScriptCommandSpec Spec { get; }

        public string Check(IReadOnlyDictionary<string, object> lit)
        {
            object a, b;
            if (lit.TryGetValue("轴A", out a) && lit.TryGetValue("轴B", out b) && string.Equals((string)a, (string)b, StringComparison.OrdinalIgnoreCase))
                return "轴A与轴B不能相同。";
            return null;
        }

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            string ax = a.Get<string>("轴A"), bx = a.Get<string>("轴B");
            double ta = a.Get<double>("目标A"), tb = a.Get<double>("目标B"), v = a.Get<double>("速度");
            return _gate.RunAutoAsync("LINE2", ax + "," + bx, () => _motion.Line2Async(ax, ta, bx, tb, v, ct));
        }
    }

    /// <summary>
    /// 多轴同时运动，一行完成。轴组格式：轴:位置[:速度[:R]];…  位置可写 @Root.Child，末尾 R 表示相对运动。
    /// 例如 "X:100:80;Y:@Pos.Y;Z:5::R"。任一轴失败 → 取消同组其余（服务立即停轴），并抛出真实故障。
    /// 轴组格式错误在编译阶段（运行前）就报出。
    /// </summary>
    public sealed class MoveMultiCommand : IScriptCommand, IScriptCommandCheck
    {
        private readonly IMotionService _motion;
        private readonly IActionGateway _gate;

        public MoveMultiCommand(IMotionService motion, IActionGateway gate) { _motion = motion; _gate = gate; }

        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "MOVE_MULTI",
            Group = "轴控制",
            Description = "多轴同时运动",
            Params = new[]
            {
             new ScriptParamSpec { Name = "轴组", Type = typeof(string),
                 Description = "轴:位置[:速度[:R]];…  如 \"X:100:80;Y:@Pos.Y;Z:5::R\"" }
         }
        };

        private sealed class Item { public string Axis, BindKey; public double Pos; public double? Vel; public bool Relative; }

        public string Check(IReadOnlyDictionary<string, object> lit)
        {
            object v;
            if (!lit.TryGetValue("轴组", out v)) return null;
            try { Parse((string)v); return null; }
            catch (InvalidOperationException ex) { return ex.Message; }
        }

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            List<Item> items = Parse(a.Get<string>("轴组"));
            var data = ctx.Get<IDataContainer>();
            foreach (Item i in items)               // 先解析全部绑定：任何一个读不到，整体都不动
            {
                if (i.BindKey == null) continue;
                double d;
                if (!data.TryGet<double>(i.BindKey, out d)) throw new InvalidOperationException("读取绑定值失败：@" + i.BindKey);
                i.Pos = d;
            }

            var work = items.Select(i => (Func<CancellationToken, Task>)(t => _motion.MoveAsync(i.Axis, i.Pos, i.Relative, i.Vel, t))).ToList();
            return _gate.RunAutoAsync("MOVE_MULTI", string.Join(",", items.Select(i => i.Axis)), () => ParallelRun.AllAsync(work, ct));
        }

        private static List<Item> Parse(string text)
        {
            var list = new List<Item>();
            foreach (string part in (text ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = part.Split(':');
                if (f.Length < 2 || f.Length > 4 || string.IsNullOrWhiteSpace(f[0]))
                    throw new InvalidOperationException("轴组格式错误：" + part + "（应为 轴:位置[:速度[:R]]）");

                var it = new Item { Axis = f[0].Trim() };
                string pos = f[1].Trim();
                if (pos.StartsWith("@"))
                {
                    string r, c;
                    it.BindKey = pos.Substring(1);
                    if (!DataKey.TrySplit(it.BindKey, out r, out c)) throw new InvalidOperationException("绑定格式应为 @Root.Child：" + pos);
                }
                else if (!double.TryParse(pos, NumberStyles.Float, CultureInfo.InvariantCulture, out it.Pos))
                    throw new InvalidOperationException("位置无效：" + part);

                if (f.Length > 2 && f[2].Trim().Length > 0)
                {
                    double vel;
                    if (!double.TryParse(f[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out vel) || vel <= 0)
                        throw new InvalidOperationException("速度无效：" + part);
                    it.Vel = vel;
                }
                if (f.Length > 3 && f[3].Trim().Length > 0)
                {
                    if (!string.Equals(f[3].Trim(), "R", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("模式只能是 R（相对）或留空：" + part);
                    it.Relative = true;
                }
                list.Add(it);
            }
            if (list.Count == 0) throw new InvalidOperationException("轴组不能为空。");
            var dup = list.GroupBy(i => i.Axis, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new InvalidOperationException("轴组内同一根轴出现多次：" + dup.Key);
            return list;
        }
    }

    public sealed class SetOutCommand : IScriptCommand
    {
        private readonly IMotionService _motion;
        private readonly IActionGateway _gate;

        public SetOutCommand(IMotionService motion, IActionGateway gate)
        {
            _motion = motion; _gate = gate;
            Spec = new ScriptCommandSpec
            {
                Name = "SET_OUT",
                Group = "IO",
                Description = "设置输出",
                Params = new[]
                {
                 new ScriptParamSpec { Name = "输出名", Type = typeof(string), Suggest = () => motion.Outputs.Select(o => o.Name) },
                 new ScriptParamSpec { Name = "值", Type = typeof(bool), Description = "ON/OFF、1/0 或 true/false" }
             }
            };
        }

        public ScriptCommandSpec Spec { get; }

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            string name = a.Get<string>("输出名"); bool on = a.Get<bool>("值");
            return _gate.RunAutoAsync("SET_OUT", name, () => { _motion.SetOutput(name, on); return Task.CompletedTask; });
        }
    }

    public sealed class WaitInCommand : IScriptCommand
    {
        private readonly IMotionService _motion;

        public WaitInCommand(IMotionService motion)
        {
            _motion = motion;
            Spec = new ScriptCommandSpec
            {
                Name = "WAIT_IN",
                Group = "IO",
                Description = "等待输入达到指定值，超时报错",
                Params = new[]
                {
                 new ScriptParamSpec { Name = "输入名", Type = typeof(string), Suggest = () => motion.Inputs.Select(i => i.Name) },
                 new ScriptParamSpec { Name = "值", Type = typeof(bool) },
                 new ScriptParamSpec { Name = "超时ms", Type = typeof(int), Required = false, Default = 5000, Description = "0 表示一直等" }
             }
            };
        }

        public ScriptCommandSpec Spec { get; }

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        { return _motion.WaitInputAsync(a.Get<string>("输入名"), a.Get<bool>("值"), a.Get<int>("超时ms"), ct); }
    }
}

using DM.Core.Actions;
using DM.Core.Common;
using DM.Core.Data;
using DM.Core.TaskList;
using DM.Engine.Common;
using DM.Engine.TaskList;
using DM.Motion.Services;
using DM.Scripting;
using DM.Scripting.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Tasks
{
    public sealed class MultiAxisItem : BindableBase
    {
        private bool _enabled = true, _relative, _useBinding;
        private int _priority = 1;
        private string _axis = "", _bindKey = "";
        private double _position, _velocity;

        public bool Enabled { get { return _enabled; } set { SetProperty(ref _enabled, value); } }
        public int Priority { get { return _priority; } set { SetProperty(ref _priority, value); } }       // 小的先执行，相同的同时执行
        public string Axis { get { return _axis; } set { SetProperty(ref _axis, value ?? ""); } }
        public bool IsRelative { get { return _relative; } set { SetProperty(ref _relative, value); } }
        public double Position { get { return _position; } set { SetProperty(ref _position, value); } }
        public double Velocity { get { return _velocity; } set { SetProperty(ref _velocity, value); } }   // 0 = 轴默认速度
        public bool UseBinding { get { return _useBinding; } set { SetProperty(ref _useBinding, value); } }
        public string BindKey { get { return _bindKey; } set { SetProperty(ref _bindKey, value ?? ""); } }
        public ObservableCollection<IoStep> Before { get; } = new ObservableCollection<IoStep>();
        public ObservableCollection<IoStep> After { get; } = new ObservableCollection<IoStep>();
    }

    public sealed class MultiAxisData : BindableBase
    {
        public ObservableCollection<MultiAxisItem> Items { get; } = new ObservableCollection<MultiAxisItem>();
    }

    /// <summary>
    /// 同优先级并行、不同优先级按升序串行。每个优先级组：
    /// 组内全部前置 IO → 全部轴同时运动 → 组内全部后置 IO。校验、执行、转换共用 Plan，预览与实际运行不会不一致。
    /// </summary>
    public sealed class MultiAxisTaskType : JsonTaskType<MultiAxisData>, ITaskConvertible
    {
        public const string TypeKey = "multi-axis";
        public MultiAxisTaskType() : base(TypeKey, "多轴运动", "轴控制", "MultiAxisEditorView") { }

        public string TargetTypeKey => ScriptTaskType.TypeKey;
        public string Convert(string data) { return ToScript(Plan(TaskJson.Deserialize<MultiAxisData>(data))); }

        private static List<List<MultiAxisItem>> Plan(MultiAxisData d)
        {
            List<MultiAxisItem> items = d.Items.Where(x => x != null && x.Enabled).ToList();
            if (items.Count == 0) throw new InvalidOperationException("没有启用的轴动作。");

            foreach (MultiAxisItem i in items)
            {
                string axis = MotionScript.Need(i.Axis, "轴");
                if (i.Priority <= 0) throw new InvalidOperationException("轴 " + axis + "：优先级必须大于 0。");
                if (i.Velocity < 0) throw new InvalidOperationException("轴 " + axis + "：速度不能为负数。");
                string e = (i.UseBinding ? MotionBinding.CheckKey(i.BindKey, "轴 " + axis + " 位置绑定键") : null)
                           ?? IoStepRunner.Check(i.Before, "轴 " + axis + " 前置动作")
                           ?? IoStepRunner.Check(i.After, "轴 " + axis + " 后置动作");
                if (e != null) throw new InvalidOperationException(e);
            }

            var groups = items.GroupBy(x => x.Priority).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            foreach (List<MultiAxisItem> g in groups)
            {
                var dup = g.GroupBy(x => x.Axis.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
                if (dup != null) throw new InvalidOperationException("同一优先级 " + g[0].Priority + " 内轴 " + dup.Key + " 重复。");
            }
            return groups;
        }

        protected override string Check(MultiAxisData d)
        {
            try { Plan(d); return null; }
            catch (InvalidOperationException ex) { return ex.Message; }
        }

        protected override async Task RunAsync(MultiAxisData d, IRunContext ctx, CancellationToken ct)
        {
            List<List<MultiAxisItem>> groups = Plan(d);
            var motion = ctx.Get<IMotionService>();
            var gate = ctx.Get<IActionGateway>();
            var data = ctx.Get<IDataContainer>();

            foreach (List<MultiAxisItem> g in groups)
            {
                ct.ThrowIfCancellationRequested();
                // 整组先读绑定值：任何一个读不到，这一组的前置动作和运动都不会发生
                var targets = g.Select(i => new { Item = i, Pos = i.UseBinding ? MotionBinding.Read(data, i.BindKey.Trim()) : i.Position }).ToList();
                string axes = string.Join(",", g.Select(i => i.Axis.Trim()));
                ctx.Log("多轴分组 Priority=" + g[0].Priority + "：" + axes);

                foreach (MultiAxisItem i in g) await IoStepRunner.RunAsync(i.Before, motion, gate, ct);

                var work = targets.Select(t =>
                {
                    string axis = t.Item.Axis.Trim(); double pos = t.Pos; bool rel = t.Item.IsRelative;
                    double? vel = t.Item.Velocity > 0 ? t.Item.Velocity : (double?)null;
                    return (Func<CancellationToken, Task>)(c => motion.MoveAsync(axis, pos, rel, vel, c));
                }).ToList();
                await gate.RunAutoAsync("MOVE_MULTI", axes, () => ParallelRun.AllAsync(work, ct));      // 一根失败，同组其余立即停

                foreach (MultiAxisItem i in g) await IoStepRunner.RunAsync(i.After, motion, gate, ct);
            }
        }

        private static string ToScript(List<List<MultiAxisItem>> groups)
        {
            var lines = new List<string>();
            foreach (List<MultiAxisItem> g in groups)
            {
                foreach (MultiAxisItem i in g) IoStepScript.Append(lines, i.Before, "轴 " + i.Axis + " 前置动作");
                if (g.Count == 1)
                    lines.Add(MotionScript.Move(g[0].Axis, g[0].IsRelative, MotionScript.Pos(g[0].UseBinding, g[0].BindKey, g[0].Position), g[0].Velocity));
                else
                {
                    foreach (MultiAxisItem i in g)
                        if (i.Axis.IndexOfAny(new[] { ':', ';' }) >= 0) throw new InvalidOperationException("轴名不能包含 : 或 ;：" + i.Axis);
                    lines.Add(ScriptText.Line("MOVE_MULTI", ScriptText.Quoted(string.Join(";", g.Select(Field)))));
                }
                foreach (MultiAxisItem i in g) IoStepScript.Append(lines, i.After, "轴 " + i.Axis + " 后置动作");
            }
            return string.Join("\r\n", lines);
        }

        /// <summary>轴:位置[:速度[:R]]；速度空表示默认，R 表示相对运动。</summary>
        private static string Field(MultiAxisItem i)
        {
            string s = i.Axis.Trim() + ":" + MotionScript.Pos(i.UseBinding, i.BindKey, i.Position)
                     + ":" + (i.Velocity > 0 ? ScriptText.Num(i.Velocity) : "") + ":" + (i.IsRelative ? "R" : "");
            return s.TrimEnd(':');
        }

        protected override string Describe(MultiAxisData d)
        {
            var en = d.Items.Where(x => x != null && x.Enabled).ToList();
            return en.Count == 0 ? "（无启用动作）" : en.Select(x => x.Priority).Distinct().Count() + " 组 / " + en.Count + " 个轴动作";
        }
    }
}

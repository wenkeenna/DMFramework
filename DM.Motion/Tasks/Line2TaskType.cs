using DM.Core.Actions;
using DM.Core.Common;
using DM.Core.Data;
using DM.Core.TaskList;
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
    public sealed class Line2Data : BindableBase
    {
        private string _axisA = "", _axisB = "", _keyA = "", _keyB = "";
        private double _targetA, _targetB, _velocity = 50;
        private bool _bindA, _bindB;

        public string AxisA { get { return _axisA; } set { SetProperty(ref _axisA, value ?? ""); } }
        public string AxisB { get { return _axisB; } set { SetProperty(ref _axisB, value ?? ""); } }
        public double TargetA { get { return _targetA; } set { SetProperty(ref _targetA, value); } }
        public double TargetB { get { return _targetB; } set { SetProperty(ref _targetB, value); } }
        public bool BindA { get { return _bindA; } set { SetProperty(ref _bindA, value); } }
        public bool BindB { get { return _bindB; } set { SetProperty(ref _bindB, value); } }
        public string KeyA { get { return _keyA; } set { SetProperty(ref _keyA, value ?? ""); } }
        public string KeyB { get { return _keyB; } set { SetProperty(ref _keyB, value ?? ""); } }
        public double Velocity { get { return _velocity; } set { SetProperty(ref _velocity, value); } }      // 合成速度
        public ObservableCollection<IoStep> Before { get; } = new ObservableCollection<IoStep>();
        public ObservableCollection<IoStep> After { get; } = new ObservableCollection<IoStep>();
    }

    public sealed class Line2TaskType : JsonTaskType<Line2Data>, ITaskConvertible
    {
        public const string TypeKey = "line2";
        public Line2TaskType() : base(TypeKey, "直线插补", "轴控制", "Line2EditorView") { }

        public string TargetTypeKey => ScriptTaskType.TypeKey;
        public string Convert(string data) { return Build(TaskJson.Deserialize<Line2Data>(data)); }

        protected override string Check(Line2Data d)
        {
            if (string.IsNullOrWhiteSpace(d.AxisA) || string.IsNullOrWhiteSpace(d.AxisB)) return "轴A、轴B不能为空。";
            if (string.Equals(d.AxisA.Trim(), d.AxisB.Trim(), StringComparison.OrdinalIgnoreCase)) return "轴A与轴B不能相同。";
            if (d.Velocity <= 0) return "合成速度必须大于 0。";
            return (d.BindA ? MotionBinding.CheckKey(d.KeyA, "目标A绑定键") : null)
                ?? (d.BindB ? MotionBinding.CheckKey(d.KeyB, "目标B绑定键") : null)
                ?? IoStepRunner.Check(d.Before, "插补前动作") ?? IoStepRunner.Check(d.After, "插补后动作");
        }

        protected override async Task RunAsync(Line2Data d, IRunContext ctx, CancellationToken ct)
        {
            var motion = ctx.Get<IMotionService>();
            var gate = ctx.Get<IActionGateway>();
            var data = ctx.Get<IDataContainer>();
            string a = d.AxisA.Trim(), b = d.AxisB.Trim();
            double ta = d.BindA ? MotionBinding.Read(data, d.KeyA.Trim()) : d.TargetA;
            double tb = d.BindB ? MotionBinding.Read(data, d.KeyB.Trim()) : d.TargetB;
            double v = d.Velocity;

            await IoStepRunner.RunAsync(d.Before, motion, gate, ct);
            ctx.Log("LINE2 " + a + " " + ScriptText.Num(ta) + " " + b + " " + ScriptText.Num(tb));
            await gate.RunAutoAsync("LINE2", a + "," + b, () => motion.Line2Async(a, ta, b, tb, v, ct));
            await IoStepRunner.RunAsync(d.After, motion, gate, ct);
        }

        private static string Build(Line2Data d)
        {
            string a = MotionScript.Need(d.AxisA, "轴A"), b = MotionScript.Need(d.AxisB, "轴B");
            var lines = new List<string>();
            IoStepScript.Append(lines, d.Before, "插补前动作");
            lines.Add(ScriptText.Line("LINE2", ScriptText.Tok(a), MotionScript.Pos(d.BindA, d.KeyA, d.TargetA),
                                      ScriptText.Tok(b), MotionScript.Pos(d.BindB, d.KeyB, d.TargetB), ScriptText.Num(d.Velocity)));
            IoStepScript.Append(lines, d.After, "插补后动作");
            return string.Join("\r\n", lines);
        }

        protected override string Describe(Line2Data d)
        {
            return string.IsNullOrWhiteSpace(d.AxisA) || string.IsNullOrWhiteSpace(d.AxisB)
                ? "（未选择轴）" : "LINE2 " + d.AxisA + "," + d.AxisB + "  速度 " + ScriptText.Num(d.Velocity);
        }
    }
}

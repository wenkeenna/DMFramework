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
    public enum AxisMoveMode { Absolute, Relative, Home }

    public sealed class SingleAxisData : BindableBase
    {
        private string _axis = "", _bindKey = "";
        private AxisMoveMode _mode;
        private double _position, _velocity;
        private bool _useBinding;

        public string Axis { get { return _axis; } set { SetProperty(ref _axis, value ?? ""); } }
        public AxisMoveMode Mode { get { return _mode; } set { SetProperty(ref _mode, value); } }
        public double Position { get { return _position; } set { SetProperty(ref _position, value); } }
        public double Velocity { get { return _velocity; } set { SetProperty(ref _velocity, value); } }      // 0 = 轴默认速度
        public bool UseBinding { get { return _useBinding; } set { SetProperty(ref _useBinding, value); } }
        public string BindKey { get { return _bindKey; } set { SetProperty(ref _bindKey, value ?? ""); } }
        public ObservableCollection<IoStep> Before { get; } = new ObservableCollection<IoStep>();
        public ObservableCollection<IoStep> After { get; } = new ObservableCollection<IoStep>();
    }

    public sealed class SingleAxisTaskType : JsonTaskType<SingleAxisData>, ITaskConvertible
    {
        public const string TypeKey = "single-axis";
        public SingleAxisTaskType() : base(TypeKey, "单轴运动", "轴控制", "SingleAxisEditorView") { }

        public string TargetTypeKey => ScriptTaskType.TypeKey;
        public string Convert(string data) { return Build(TaskJson.Deserialize<SingleAxisData>(data)); }

        protected override string Check(SingleAxisData d)
        {
            if (string.IsNullOrWhiteSpace(d.Axis)) return "轴不能为空。";
            if (d.Velocity < 0) return "速度不能为负数。";
            if (d.Mode != AxisMoveMode.Home && d.UseBinding)
            { string e = MotionBinding.CheckKey(d.BindKey, "位置绑定键"); if (e != null) return e; }
            return IoStepRunner.Check(d.Before, "运动前动作") ?? IoStepRunner.Check(d.After, "运动后动作");
        }

        protected override async Task RunAsync(SingleAxisData d, IRunContext ctx, CancellationToken ct)
        {
            var motion = ctx.Get<IMotionService>();
            var gate = ctx.Get<IActionGateway>();
            string axis = d.Axis.Trim();
            bool relative = d.Mode == AxisMoveMode.Relative;
            // 先读绑定：读不到时前置动作还没有执行
            double pos = d.Mode == AxisMoveMode.Home ? 0 : d.UseBinding ? MotionBinding.Read(ctx.Get<IDataContainer>(), d.BindKey.Trim()) : d.Position;
            double? vel = d.Velocity > 0 ? d.Velocity : (double?)null;

            await IoStepRunner.RunAsync(d.Before, motion, gate, ct);
            if (d.Mode == AxisMoveMode.Home)
            {
                ctx.Log("HOME " + axis);
                await gate.RunAutoAsync("HOME", axis, () => motion.HomeAsync(axis, ct));
            }
            else
            {
                ctx.Log((relative ? "MOVE_REL " : "MOVE_ABS ") + axis + " " + ScriptText.Num(pos));
                await gate.RunAutoAsync(relative ? "MOVE_REL" : "MOVE_ABS", axis, () => motion.MoveAsync(axis, pos, relative, vel, ct));
            }
            await IoStepRunner.RunAsync(d.After, motion, gate, ct);
        }

        private static string Build(SingleAxisData d)
        {
            string axis = MotionScript.Need(d.Axis, "轴");
            var lines = new List<string>();
            IoStepScript.Append(lines, d.Before, "运动前动作");
            if (d.Mode == AxisMoveMode.Home) lines.Add(ScriptText.Line("HOME", ScriptText.Tok(axis)));
            else lines.Add(MotionScript.Move(axis, d.Mode == AxisMoveMode.Relative, MotionScript.Pos(d.UseBinding, d.BindKey, d.Position), d.Velocity));
            IoStepScript.Append(lines, d.After, "运动后动作");
            return string.Join("\r\n", lines);
        }

        protected override string Describe(SingleAxisData d)
        {
            if (string.IsNullOrWhiteSpace(d.Axis)) return "（未选择轴）";
            if (d.Mode == AxisMoveMode.Home) return d.Axis + " 回零";
            string pos = d.UseBinding ? "@" + d.BindKey : ScriptText.Num(d.Position);
            return d.Axis + (d.Mode == AxisMoveMode.Relative ? " 相对 " : " 绝对 ") + pos + (d.Velocity > 0 ? "  速度 " + ScriptText.Num(d.Velocity) : "");
        }
    }
}

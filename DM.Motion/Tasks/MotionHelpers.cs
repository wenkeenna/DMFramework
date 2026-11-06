using DM.Core.Actions;
using DM.Core.Data;
using DM.Motion.Services;
using DM.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Tasks
{
    /// <summary>仅用于转换：把运动参数写成脚本行。</summary>
    public static class MotionScript
    {
        public static string Need(string value, string what)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(what + "不能为空。");
            return value.Trim();
        }

        public static string Pos(bool useBinding, string key, double value) { return useBinding ? ScriptText.Bind(key) : ScriptText.Num(value); }

        /// <summary>速度为 0 表示用轴的默认速度，不写速度参数。</summary>
        public static string Move(string axis, bool relative, string posToken, double velocity)
        {
            if (velocity < 0) throw new InvalidOperationException("速度不能为负数。");
            string cmd = relative ? "MOVE_REL" : "MOVE_ABS";
            return velocity > 0 ? ScriptText.Line(cmd, ScriptText.Tok(axis), posToken, ScriptText.Num(velocity))
                                : ScriptText.Line(cmd, ScriptText.Tok(axis), posToken);
        }
    }

    public static class MotionBinding
    {
        public static string CheckKey(string key, string what)
        { string r, c; return DataKey.TrySplit((key ?? "").Trim(), out r, out c) ? null : what + "格式应为 Root.Child。"; }

        public static double Read(IDataContainer data, string key)
        {
            double v;
            if (!data.TryGet<double>(key, out v)) throw new InvalidOperationException("读取绑定值失败：" + key);
            return v;
        }
    }

    /// <summary>IO 步骤的直接执行（置位 / 等待输入 / 延时），不经过脚本。</summary>
    public static class IoStepRunner
    {
        public static string Check(IEnumerable<IoStep> steps, string owner)
        {
            if (steps == null) return null;
            foreach (IoStep s in steps)
            {
                if (s == null || !s.Enabled) continue;
                if (s.Kind != IoStepKind.Delay && string.IsNullOrWhiteSpace(s.Name)) return owner + "：存在未填写名称的 IO 步骤。";
                if (s.Kind == IoStepKind.WaitIn && s.Ms < 0) return owner + "：等待超时不能为负数。";
                if (s.Kind == IoStepKind.Delay && s.Ms <= 0) return owner + "：延时必须大于 0。";
            }
            return null;
        }

        public static async Task RunAsync(IEnumerable<IoStep> steps, IMotionService motion, IActionGateway gate, CancellationToken ct)
        {
            if (steps == null) return;
            foreach (IoStep s in steps.Where(x => x != null && x.Enabled).ToList())
            {
                ct.ThrowIfCancellationRequested();
                string name = (s.Name ?? "").Trim(); bool v = s.Value; int ms = s.Ms;
                switch (s.Kind)
                {
                    case IoStepKind.SetOut:
                        await gate.RunAutoAsync("SET_OUT", name, () => { motion.SetOutput(name, v); return Task.CompletedTask; });
                        break;
                    case IoStepKind.WaitIn: await motion.WaitInputAsync(name, v, ms, ct); break;
                    default: await Task.Delay(ms, ct); break;
                }
            }
        }
    }
}

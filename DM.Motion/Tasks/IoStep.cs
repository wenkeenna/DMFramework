using DM.Scripting;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Tasks
{
    public enum IoStepKind { SetOut, WaitIn, Delay }

    /// <summary>一个 IO 步骤：置位输出 / 等待输入 / 延时。四个运动任务的前后置动作和"IO 执行"任务共用。</summary>
    public sealed class IoStep : BindableBase
    {
        private bool _enabled = true, _value = true;
        private IoStepKind _kind;
        private string _name = "";
        private int _ms = 1000;

        public bool Enabled { get { return _enabled; } set { SetProperty(ref _enabled, value); } }
        public IoStepKind Kind { get { return _kind; } set { SetProperty(ref _kind, value); } }
        public string Name { get { return _name; } set { SetProperty(ref _name, value ?? ""); } }
        public bool Value { get { return _value; } set { SetProperty(ref _value, value); } }
        /// <summary>WaitIn：超时毫秒（0=一直等）；Delay：延时毫秒。</summary>
        public int Ms { get { return _ms; } set { SetProperty(ref _ms, value); } }
    }

    /// <summary>仅用于"转换为脚本任务"：把 IO 步骤写成脚本行。</summary>
    public static class IoStepScript
    {
        public static int CountEnabled(IEnumerable<IoStep> steps) { return steps == null ? 0 : steps.Count(s => s != null && s.Enabled); }

        public static void Append(List<string> lines, IEnumerable<IoStep> steps, string owner)
        {
            if (steps == null) return;
            foreach (IoStep s in steps)
            {
                if (s == null || !s.Enabled) continue;
                switch (s.Kind)
                {
                    case IoStepKind.SetOut:
                        lines.Add(ScriptText.Line("SET_OUT", ScriptText.Tok(Need(s, owner)), ScriptText.OnOff(s.Value)));
                        break;
                    case IoStepKind.WaitIn:
                        if (s.Ms < 0) throw new InvalidOperationException(owner + "：等待超时不能为负数。");
                        lines.Add(ScriptText.Line("WAIT_IN", ScriptText.Tok(Need(s, owner)), ScriptText.OnOff(s.Value), ScriptText.Num(s.Ms)));
                        break;
                    default:
                        if (s.Ms <= 0) throw new InvalidOperationException(owner + "：延时必须大于 0。");
                        lines.Add(ScriptText.Line("DELAY", ScriptText.Num(s.Ms)));
                        break;
                }
            }
        }

        private static string Need(IoStep s, string owner)
        {
            if (string.IsNullOrWhiteSpace(s.Name)) throw new InvalidOperationException(owner + "：存在未填写名称的 IO 步骤。");
            return s.Name;
        }
    }
}

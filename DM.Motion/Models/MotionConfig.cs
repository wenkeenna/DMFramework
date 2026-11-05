using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Models
{
    public sealed class MotionConfig
    {
        public string Driver { get; set; }                    // "Simulator" / "Leadshine"
        public List<AxisConfig> Axes { get; set; } = new List<AxisConfig>();
        public List<IoConfig> Inputs { get; set; } = new List<IoConfig>();
        public List<IoConfig> Outputs { get; set; } = new List<IoConfig>();

        /// <summary>返回所有配置错误，连接设备前一次报全。</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            if (string.IsNullOrWhiteSpace(Driver)) e.Add("machine.json 未配置 Motion.Driver。");
            foreach (AxisConfig a in Axes)
            {
                if (string.IsNullOrWhiteSpace(a.Name)) e.Add("存在未命名的轴。");
                if (a.PulsePerUnit <= 0) e.Add("轴 " + a.Name + "：PulsePerUnit 必须大于 0。");
                if (a.MaxVelocity <= 0) e.Add("轴 " + a.Name + "：MaxVelocity 必须大于 0。");
            }
            Dup(Axes.Select(a => a.Name), "轴", e);
            Dup(Inputs.Select(i => i.Name), "输入", e);
            Dup(Outputs.Select(o => o.Name), "输出", e);
            return e;
        }

        private static void Dup(IEnumerable<string> names, string kind, List<string> e)
        {
            foreach (var g in names.Where(n => !string.IsNullOrWhiteSpace(n))
                                   .GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                e.Add(kind + "名称重复：" + g.Key);
        }
    }

    public struct AxisState { public double PositionPulse; public bool IsMoving, Alarm, Homed; }
    public struct MoveParams { public double VelocityPulse, AccTime, DecTime; }


}

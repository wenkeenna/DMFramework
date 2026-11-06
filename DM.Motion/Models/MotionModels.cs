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

namespace DM.Motion.Models
{
    public sealed class AxisConfig
    {
        public string Name { get; set; }
        public int Card { get; set; }
        public int Index { get; set; }
        public double PulsePerUnit { get; set; } = 1000;      // 脉冲 / 单位
        public double MaxVelocity { get; set; } = 100;        // 单位 / 秒
        public double DefaultVelocity { get; set; } = 50;
        public double AccTime { get; set; } = 0.1;            // 秒
        public double DecTime { get; set; } = 0.1;
        public double? MinPos { get; set; }                   // 软限位，空表示不限
        public double? MaxPos { get; set; }
        public double InPosTolerance { get; set; } = 0.01;    // 到位容差（单位）
        public int HomeMode { get; set; }                     // 由驱动解释
        public double HomeVelocity { get; set; } = 10;
        public double HomeOffset { get; set; }          // 回零完成后相对移动的距离（单位），0 表示不偏移
    }

    public sealed class IoConfig
    {
        public string Name { get; set; }
        public int Card { get; set; }
        public int Bit { get; set; }
        public bool Inverted { get; set; }                    // 低电平有效
    }

    public sealed class MotionConfig
    {
        public string Driver { get; set; }                    // 驱动名，如 "Leadshine"
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
                string n = string.IsNullOrWhiteSpace(a.Name) ? "(未命名)" : a.Name;
                if (string.IsNullOrWhiteSpace(a.Name)) e.Add("存在未命名的轴。");
                if (a.PulsePerUnit <= 0) e.Add("轴 " + n + "：PulsePerUnit 必须大于 0。");
                if (a.MaxVelocity <= 0) e.Add("轴 " + n + "：MaxVelocity 必须大于 0。");
                if (a.DefaultVelocity <= 0 || a.DefaultVelocity > a.MaxVelocity) e.Add("轴 " + n + "：DefaultVelocity 必须在 (0, MaxVelocity] 内。");
                if (a.AccTime < 0 || a.DecTime < 0) e.Add("轴 " + n + "：加减速时间不能为负数。");
                if (a.InPosTolerance <= 0) e.Add("轴 " + n + "：InPosTolerance 必须大于 0。");
                if (a.MinPos.HasValue && a.MaxPos.HasValue && a.MinPos.Value > a.MaxPos.Value) e.Add("轴 " + n + "：MinPos 大于 MaxPos。");
                if (a.Card < 0 || a.Index < 0) e.Add("轴 " + n + "：Card、Index 不能为负数。");
                if (a.HomeVelocity <= 0) e.Add("轴 " + n + "：HomeVelocity 必须大于 0。");
            }

            foreach (IoConfig io in Inputs.Concat(Outputs))
                if (io.Card < 0 || io.Bit < 0) e.Add("IO " + io.Name + "：Card、Bit 不能为负数。");



            Dup(Axes.Select(a => a.Name), "轴名称", e);
            Dup(Inputs.Select(i => i.Name), "输入名称", e);
            Dup(Outputs.Select(o => o.Name), "输出名称", e);
            Dup(Axes.Select(a => a.Card + ":" + a.Index), "轴硬件位置(Card:Index)", e);
            Dup(Inputs.Select(i => i.Card + ":" + i.Bit), "输入点位(Card:Bit)", e);
            Dup(Outputs.Select(o => o.Card + ":" + o.Bit), "输出点位(Card:Bit)", e);
            return e;
        }

        private static void Dup(IEnumerable<string> names, string kind, List<string> e)
        {
            foreach (var g in names.Where(n => !string.IsNullOrWhiteSpace(n))
                                   .GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                e.Add(kind + "重复：" + g.Key);
        }
    }

    public struct AxisState { public double PositionPulse; public bool IsMoving, Alarm, Homed; }
    public struct MoveParams { public double VelocityPulse, AccTime, DecTime; }

    /// <summary>给界面显示用，位置已换算成单位。</summary>
    public sealed class AxisStatus
    {
        public string Name { get; set; }
        public double Position { get; set; }
        public bool IsMoving { get; set; }
        public bool Alarm { get; set; }
        public bool Homed { get; set; }
    }
}

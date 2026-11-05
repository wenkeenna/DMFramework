using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Models
{
    public sealed class AxisConfig
    {
        public string Name { get; set; }
        public int Card { get; set; }
        public int Index { get; set; }
        public double PulsePerUnit { get; set; } = 1000;      // 脉冲/单位
        public double MaxVelocity { get; set; } = 100;        // 单位/秒
        public double DefaultVelocity { get; set; } = 50;
        public double AccTime { get; set; } = 0.1;            // 秒
        public double DecTime { get; set; } = 0.1;
        public double? MinPos { get; set; }                   // 软限位，空表示不限
        public double? MaxPos { get; set; }
        public double InPosTolerance { get; set; } = 0.01;    // 到位容差（单位）
        public int HomeMode { get; set; }                     // 由驱动解释
        public double HomeVelocity { get; set; } = 10;
    }
}

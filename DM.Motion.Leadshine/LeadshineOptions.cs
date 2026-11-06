using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Leadshine
{
    /// <summary>machine.json 的 "Leadshine" 段。</summary>
    public sealed class LeadshineOptions
    {
        /// <summary>卡号 → 配置文件路径。相对路径相对于 Config 目录。用到的每张卡都必须配置。</summary>
        public Dictionary<int, string> CardConfigFiles { get; set; } = new Dictionary<int, string>();

        /// <summary>外部急停按钮对应的输入点名（Motion.Inputs 里的名字，Inverted 要配成"按下为 true"）。留空表示不启用外部急停监控。</summary>
        public string EmergencyInput { get; set; } = "";
        public int EmergencyPollMs { get; set; } = 50;
        /// <summary>读取急停输入连续失败时按安全侧处理（触发急停），避免监控悄悄失效。</summary>
        public bool EmergencyFailSafe { get; set; } = true;

        /// <summary>连接成功后自动给所有轴上使能。使能失败只记警告，不影响其他轴。</summary>
        public bool AutoEnable { get; set; } = true;

        /// <summary>速度曲线的起始/结束速度占最高速的比例（沿用旧版的 0.3）。</summary>
        public double StartVelocityRatio { get; set; } = 0.3;

        /// <summary>直线插补使用的坐标系号。</summary>
        public int LineCoordinate { get; set; }

        public string Check()
        {
            if (StartVelocityRatio <= 0 || StartVelocityRatio > 1) return "StartVelocityRatio 必须在 (0, 1] 内。";
            if (LineCoordinate < 0) return "LineCoordinate 不能为负数。";
            if (EmergencyPollMs < 10) return "EmergencyPollMs 不能小于 10。";
            return null;
        }
    }
}

using DM.Motion.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Customer.X.Pack.Motion
{
    /// <summary>轴名常量：任务、脚本、代码里引用轴时用，避免写错字符串。</summary>
    public static class Axes
    {
        public const string Gx = "Gx";
        public const string Gy = "Gy";
        public const string GlueZ = "Glue";
        public const string LeftZ = "L_Z";
        public const string RightZ = "R_Z";
    }

    /// <summary>IO 名常量。</summary>
    public static class Io
    {
        // 输入
        public const string EStop = "急停按钮";
        public const string GlueDown = "点胶下限";
        public const string GlueUp = "点胶上限";
        public const string Vacuum = "真空到位";
        // 输出
        public const string GlueCylinder = "点胶气缸";
        public const string GlueValve = "点胶机";
        public const string VacuumOn = "真空阀";
    }

    /// <summary>X 客户点胶机的运动配置。改这个类需要重新编译发布。</summary>
    public sealed class XMotionProfile : MotionProfileBase
    {
        public override string Name => "X 客户点胶机 V2";

        protected override void Define()
        {
            Driver("Leadshine");

            // ───── 轴：名称, 卡号, 轴号, 脉冲/单位, 最高速度 ─────
            Axis(Axes.Gx, card: 0, index: 0, pulsePerUnit: 1000, maxVelocity: 300)
                .SoftLimit(0, 300).DefaultVelocity(100).AccDec(0.1, 0.1).Home(velocity: 20, offset: 1.0);

            Axis(Axes.Gy, card: 0, index: 1, pulsePerUnit: 1000, maxVelocity: 300)
                .SoftLimit(0, 300).DefaultVelocity(100).AccDec(0.1, 0.1).Home(velocity: 20, offset: 1.0);

            Axis(Axes.GlueZ, card: 0, index: 2, pulsePerUnit: 2000, maxVelocity: 100)
                .SoftLimit(0, 80).DefaultVelocity(30).AccDec(0.15, 0.15).InPosTolerance(0.005)
                .Home(velocity: 10, offset: 0.5);

            Axis(Axes.LeftZ, card: 1, index: 0, pulsePerUnit: 2000, maxVelocity: 80)
                .SoftLimit(0, 60).DefaultVelocity(20).AccDec(0.2, 0.2);

            Axis(Axes.RightZ, card: 1, index: 1, pulsePerUnit: 2000, maxVelocity: 80)
                .SoftLimit(0, 60).DefaultVelocity(20).AccDec(0.2, 0.2);

            // ───── 输入：名称, 卡号, 位号, [低电平有效] ─────
            Input(Io.EStop, card: 0, bit: 0, inverted: true);       // 急停按钮：常闭，按下读到 0，取反后"按下 = true"
            Input(Io.GlueDown, card: 0, bit: 3);
            Input(Io.GlueUp, card: 0, bit: 4);
            Input(Io.Vacuum, card: 0, bit: 5);

            // ───── 输出 ─────
            Output(Io.GlueCylinder, card: 0, bit: 2);
            Output(Io.GlueValve, card: 0, bit: 5);
            Output(Io.VacuumOn, card: 0, bit: 6);
        }
    }
}

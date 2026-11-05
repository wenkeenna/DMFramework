using DM.Motion.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Services
{
    /// <summary>
    /// 驱动只做"下发命令 + 读状态"，全部同步、立即返回。
    /// 等待到位、超时、取消、软限位、到位判断都在 MotionService，驱动里不要写。
    /// </summary>
    public interface IMotionDriver
    {
        void Open(MotionConfig cfg);                   // 可能很慢，由设备中心放在线程池里调用
        void Close();
        void StartMove(AxisConfig a, double targetPulse, bool relative, MoveParams p);
        void StartHome(AxisConfig a);
        void Stop(AxisConfig a, bool emergency);       // 必须能在任意线程调用且立即生效
        AxisState GetState(AxisConfig a);
        bool ReadInput(IoConfig io);                   // 原始电平，反相由服务处理
        bool ReadOutput(IoConfig io);
        void WriteOutput(IoConfig io, bool level);
    }
}

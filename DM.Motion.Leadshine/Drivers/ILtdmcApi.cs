using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Leadshine.Drivers
{
    /// <summary>
    /// LTDMC 静态 SDK 包装接口。
    /// 
    /// 这样上层控制器依赖的是接口，不是直接依赖静态类。
    /// </summary>
    public interface ILtdmcApi
    {
        short BoardInit();
        short DownloadConfigFile(ushort cardIndex, string filePath);

        short SetProfile(ushort card, ushort axis, double minVel, double maxVel, double acc, double dec, double stopVel);
        short SetSProfile(ushort card, ushort axis, ushort mode, double sTime);
        short PMove(ushort card, ushort axis, int position, ushort absoluteMode);
        short VMove(ushort card, ushort axis, ushort direction);

        short SetVectorProfileMulticoor(ushort card, ushort crd, double minVel, double maxVel, double tAcc, double tDec, double stopVel);
        short SetVectorSProfile(ushort card, ushort crd, ushort mode, double sPara);
        short LineMulticoor(ushort card, ushort crd, ushort axisNum, ushort[] axisList, int[] targetPos, ushort posiMode);

        short Stop(ushort card, ushort axis, ushort stopMode);

        short StopMulticoor(ushort card, ushort crd, ushort stopMode);

        short HomeMove(ushort card, ushort axis);
        short SetPosition(ushort card, ushort axis, int position);

        int GetPosition(ushort card, ushort axis);
        int GetEncoder(ushort card, ushort axis);
        short CheckDone(ushort card, ushort axis);

        uint GetAxisIoStatus(ushort card, ushort axis);
        short WriteSevonPin(ushort card, ushort axis, ushort level);
        short ReadSevonPin(ushort card, ushort axis);
        short WriteErcPin(ushort card, ushort axis, ushort level);
        short ReadErcPin(ushort card, ushort axis);

        short SetSoftLimit(ushort card, ushort axis, ushort enable, ushort sourceSel, ushort action, int minPulse, int maxPulse);

        /// <summary>
        /// 读取输入点状态。
        /// 返回值通常 0/1。
        /// </summary>
        short ReadInBit(ushort card, ushort bitNo);

        /// <summary>
        /// 写输出点状态。
        /// onOff:
        /// 0 = 关
        /// 1 = 开
        /// </summary>
        short WriteOutBit(ushort card, ushort bitNo, ushort onOff);

        /// <summary>
        /// 读取输出点状态。
        /// 返回值通常 0/1。
        /// </summary>
        short ReadOutBit(ushort card, ushort bitNo);

        /// <summary>
        /// 设置EMG急停信号
        /// </summary>
        /// <param name="card"></param>
        /// <param name="axis"></param>
        /// <param name="enable"></param>
        /// <param name="emg_logic"></param>
        /// <returns></returns>
        short SetEmgMode(ushort card, ushort axis,ushort enable,ushort emg_logic);

        /// <summary>
        /// 设置轴 IO 映射关系
        /// </summary>
        /// <param name="card"></param>
        /// <param name="axis"></param>
        /// <param name="iotype"></param>
        /// <param name="mapiotype"></param>
        /// <param name="mapioindex"></param>
        /// <returns></returns>
        short SetAxisIoMap(ushort card, ushort axis, ushort iotype, ushort mapiotype, ushort mapioindex, double filter_time=0.1);

    }
}

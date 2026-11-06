using DM.Motion.Leadshine.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Leadshine.Drivers
{
    /// <summary>
    /// LTDMC 静态 SDK 包装实现。
    /// 
    /// 这个类的职责非常单纯：
    /// 1. 把静态 SDK 包一层接口
    /// 2. 让上层控制器不直接依赖 LTDMC 静态类
    /// 3. 为以后做单元测试、替换控制卡实现留出边界
    /// 
    /// 注意：
    /// 这一层只做“薄包装”，
    /// 不在这里写业务逻辑、超时等待、速度曲线策略。
    /// </summary>
    public sealed class LtdmcApi : ILtdmcApi, IMotionCardDriver
    {
        /// <summary>
        /// 初始化控制卡。
        /// 
        /// 返回值语义请你再结合你现在现场实际测试确认：
        /// 大多数场景下，返回值大于 0 表示检测到控制卡数量。
        /// </summary>
        public short BoardInit()
        {
            return LTDMC.dmc_board_init();
        }

        /// <summary>
        /// 下载配置文件。
        /// </summary>
        public short DownloadConfigFile(ushort cardIndex, string filePath)
        {
            return LTDMC.dmc_download_configfile(cardIndex, filePath);
        }

        /// <summary>
        /// 设置速度曲线。
        /// </summary>
        public short SetProfile(ushort card, ushort axis, double minVel, double maxVel, double acc, double dec, double stopVel)
        {
            return LTDMC.dmc_set_profile(card, axis, minVel, maxVel, acc, dec, stopVel);
        }

        /// <summary>
        /// 设置 S 曲线。
        /// </summary>
        public short SetSProfile(ushort card, ushort axis, ushort mode, double sTime)
        {
            return LTDMC.dmc_set_s_profile(card, axis, mode, sTime);
        }

        /// <summary>
        /// 点位运动。
        /// absoluteMode:
        /// 0 = 相对运动
        /// 1 = 绝对运动
        /// </summary>
        public short PMove(ushort card, ushort axis, int position, ushort absoluteMode)
        {
            return LTDMC.dmc_pmove(card, axis, position, absoluteMode);
        }

        /// <summary>
        /// 点动运动。
        /// </summary>
        public short VMove(ushort card, ushort axis, ushort direction)
        {
            return LTDMC.dmc_vmove(card, axis, direction);
        }

        public short SetVectorProfileMulticoor(ushort card, ushort crd, double minVel, double maxVel, double tAcc, double tDec, double stopVel)
        {
            return LTDMC.dmc_set_vector_profile_multicoor(card, crd, minVel, maxVel, tAcc, tDec, stopVel);
        }

        public short SetVectorSProfile(ushort card, ushort crd, ushort mode, double sPara)
        {
            return LTDMC.dmc_set_vector_s_profile(card, crd, mode, sPara);
        }

        public short LineMulticoor(ushort card, ushort crd, ushort axisNum, ushort[] axisList, int[] targetPos, ushort posiMode)
        {
            return LTDMC.dmc_line_multicoor(card, crd, axisNum, axisList, targetPos, posiMode);
        }


        /// <summary>
        /// 停止运动。
        /// </summary>
        public short Stop(ushort card, ushort axis, ushort stopMode)
        {
            return LTDMC.dmc_stop(card, axis, stopMode);
        }

        /// <summary>
        /// 停止坐标系内所有轴的运动
        /// </summary>
        /// <param name="card">控制卡卡号</param>
        /// <param name="axis">坐标系号</param>
        /// <param name="stopMode">制动方式，0：减速停止，1：立即停止</param>
        /// <returns></returns>
        public short StopMulticoor(ushort card, ushort crd, ushort stopMode)
        {
            return LTDMC.dmc_stop_multicoor(card, crd, stopMode);
        }

        /// <summary>
        /// 启动回零。
        /// </summary>
        public short HomeMove(ushort card, ushort axis)
        {
            return LTDMC.dmc_home_move(card, axis);
        }

        /// <summary>
        /// 置当前位置。
        /// </summary>
        public short SetPosition(ushort card, ushort axis, int position)
        {
            return LTDMC.dmc_set_position(card, axis, position);
        }

        /// <summary>
        /// 读取命令位置。
        /// </summary>
        public int GetPosition(ushort card, ushort axis)
        {
            return LTDMC.dmc_get_position(card, axis);
        }

        /// <summary>
        /// 读取编码器位置。
        /// </summary>
        public int GetEncoder(ushort card, ushort axis)
        {
            return LTDMC.dmc_get_encoder(card, axis);
        }

        /// <summary>
        /// 检查运动是否完成。
        /// 1 表示完成，0 表示未完成。
        /// </summary>
        public short CheckDone(ushort card, ushort axis)
        {
            return LTDMC.dmc_check_done(card, axis);
        }

        public short SetEquiv(ushort card, ushort axis, double equiv)
        {
            return LTDMC.dmc_set_equiv(card, axis, equiv);
        }

        public short GetEquiv(ushort card, ushort axis, ref double equiv)
        {
            return LTDMC.dmc_get_equiv(card, axis, ref equiv);
        }


        /// <summary>
        /// 读取轴 IO 状态。
        /// </summary>
        public uint GetAxisIoStatus(ushort card, ushort axis)
        {
            return LTDMC.dmc_axis_io_status(card, axis);
        }

        /// <summary>
        /// 写使能引脚。
        /// </summary>
        public short WriteSevonPin(ushort card, ushort axis, ushort level)
        {
            return LTDMC.dmc_write_sevon_pin(card, axis, level);
        }

        /// <summary>
        /// 读使能引脚。
        /// </summary>
        public short ReadSevonPin(ushort card, ushort axis)
        {
            return LTDMC.dmc_read_sevon_pin(card, axis);
        }

        /// <summary>
        /// 写 ERC 引脚。
        /// </summary>
        public short WriteErcPin(ushort card, ushort axis, ushort level)
        {
            return LTDMC.dmc_write_erc_pin(card, axis, level);
        }

        /// <summary>
        /// 读 ERC 引脚。
        /// </summary>
        public short ReadErcPin(ushort card, ushort axis)
        {
            return LTDMC.dmc_read_erc_pin(card, axis);
        }

        /// <summary>
        /// 设置软限位。
        /// </summary>
        public short SetSoftLimit(ushort card, ushort axis, ushort enable, ushort sourceSel, ushort action, int minPulse, int maxPulse)
        {
            return LTDMC.dmc_set_softlimit(card, axis, enable, sourceSel, action, minPulse, maxPulse);
        }

        public short SetEmgMode(ushort card, ushort axis, ushort enable, ushort emg_logic)
        {
            return LTDMC.dmc_set_emg_mode(card, axis, enable, emg_logic);
        }



        /// <summary>
        /// IMotionCardDriver.Initialize 实现。
        /// 
        /// 这里只返回是否成功，
        /// 更详细的错误信息由上层装配器统一包装。
        /// </summary>
        bool IMotionCardDriver.Initialize()
        {
            short result = BoardInit();
            return result > 0;
        }

        /// <summary>
        /// IMotionCardDriver.DownloadConfigFile 实现。
        /// </summary>
        bool IMotionCardDriver.DownloadConfigFile(int cardIndex, string filePath)
        {
            return DownloadConfigFile((ushort)cardIndex, filePath) == 0;
        }

        /// <summary>
        /// IMotionCardDriver.EmergencyStopAsync 实现。
        /// </summary>
        Task IMotionCardDriver.EmergencyStopAsync(int cardIndex, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            short result = LTDMC.dmc_emg_stop((ushort)cardIndex);
            if (result != 0)
            {
                throw new InvalidOperationException("急停失败，错误码：" + result);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 读取输入点状态。
        /// </summary>
        public short ReadInBit(ushort card, ushort bitNo)
        {
            return LTDMC.dmc_read_inbit(card, bitNo);
        }

        /// <summary>
        /// 写输出点状态。
        /// </summary>
        public short WriteOutBit(ushort card, ushort bitNo, ushort onOff)
        {
            return LTDMC.dmc_write_outbit(card, bitNo, onOff);
        }

        /// <summary>
        /// 读取输出点状态。
        /// </summary>
        public short ReadOutBit(ushort card, ushort bitNo)
        {
            return LTDMC.dmc_read_outbit(card, bitNo);
        }

        public short SetAxisIoMap(ushort card, ushort axis, ushort iotype, ushort mapiotype, ushort mapioindex,double filter_time=0.1)
        {
            return LTDMC.dmc_set_axis_io_map (card, axis, iotype, mapiotype, mapioindex, filter_time);
        }
    }
}

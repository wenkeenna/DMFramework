using DM.Motion.Leadshine.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Leadshine.Drivers
{
    /// <summary>LTDMC 静态 SDK 的薄包装：不写业务逻辑、超时等待和速度曲线策略。</summary>
    public sealed class LtdmcApi : ILtdmcApi
    {
        public short BoardInit() { return LTDMC.dmc_board_init(); }                      // > 0 表示检测到的控制卡数量
        public short DownloadConfigFile(ushort cardIndex, string filePath) { return LTDMC.dmc_download_configfile(cardIndex, filePath); }

        public short SetProfile(ushort card, ushort axis, double minVel, double maxVel, double acc, double dec, double stopVel)
        { return LTDMC.dmc_set_profile(card, axis, minVel, maxVel, acc, dec, stopVel); }

        public short SetSProfile(ushort card, ushort axis, ushort mode, double sTime) { return LTDMC.dmc_set_s_profile(card, axis, mode, sTime); }
        public short PMove(ushort card, ushort axis, int position, ushort absoluteMode) { return LTDMC.dmc_pmove(card, axis, position, absoluteMode); }   // 0 相对，1 绝对
        public short VMove(ushort card, ushort axis, ushort direction) { return LTDMC.dmc_vmove(card, axis, direction); }

        public short SetVectorProfileMulticoor(ushort card, ushort crd, double minVel, double maxVel, double tAcc, double tDec, double stopVel)
        { return LTDMC.dmc_set_vector_profile_multicoor(card, crd, minVel, maxVel, tAcc, tDec, stopVel); }

        public short SetVectorSProfile(ushort card, ushort crd, ushort mode, double sPara) { return LTDMC.dmc_set_vector_s_profile(card, crd, mode, sPara); }

        public short LineMulticoor(ushort card, ushort crd, ushort axisNum, ushort[] axisList, int[] targetPos, ushort posiMode)
        { return LTDMC.dmc_line_multicoor(card, crd, axisNum, axisList, targetPos, posiMode); }

        public short Stop(ushort card, ushort axis, ushort stopMode) { return LTDMC.dmc_stop(card, axis, stopMode); }                       // 0 减速，1 立即
        public short StopMulticoor(ushort card, ushort crd, ushort stopMode) { return LTDMC.dmc_stop_multicoor(card, crd, stopMode); }

        public short HomeMove(ushort card, ushort axis) { return LTDMC.dmc_home_move(card, axis); }
        public short SetPosition(ushort card, ushort axis, int position) { return LTDMC.dmc_set_position(card, axis, position); }
        public int GetPosition(ushort card, ushort axis) { return LTDMC.dmc_get_position(card, axis); }
        public int GetEncoder(ushort card, ushort axis) { return LTDMC.dmc_get_encoder(card, axis); }
        public short CheckDone(ushort card, ushort axis) { return LTDMC.dmc_check_done(card, axis); }                                         // 1 已完成

        public uint GetAxisIoStatus(ushort card, ushort axis) { return LTDMC.dmc_axis_io_status(card, axis); }
        public short WriteSevonPin(ushort card, ushort axis, ushort level) { return LTDMC.dmc_write_sevon_pin(card, axis, level); }          // 0 使能
        public short ReadSevonPin(ushort card, ushort axis) { return LTDMC.dmc_read_sevon_pin(card, axis); }
        public short WriteErcPin(ushort card, ushort axis, ushort level) { return LTDMC.dmc_write_erc_pin(card, axis, level); }
        public short ReadErcPin(ushort card, ushort axis) { return LTDMC.dmc_read_erc_pin(card, axis); }

        public short SetSoftLimit(ushort card, ushort axis, ushort enable, ushort sourceSel, ushort action, int minPulse, int maxPulse)
        { return LTDMC.dmc_set_softlimit(card, axis, enable, sourceSel, action, minPulse, maxPulse); }

        public short ReadInBit(ushort card, ushort bitNo) { return LTDMC.dmc_read_inbit(card, bitNo); }
        public short WriteOutBit(ushort card, ushort bitNo, ushort onOff) { return LTDMC.dmc_write_outbit(card, bitNo, onOff); }
        public short ReadOutBit(ushort card, ushort bitNo) { return LTDMC.dmc_read_outbit(card, bitNo); }

        public short SetEmgMode(ushort card, ushort axis, ushort enable, ushort emgLogic) { return LTDMC.dmc_set_emg_mode(card, axis, enable, emgLogic); }
        public short SetAxisIoMap(ushort card, ushort axis, ushort iotype, ushort mapiotype, ushort mapioindex, double filter_time = 0.1)
        { return LTDMC.dmc_set_axis_io_map(card, axis, iotype, mapiotype, mapioindex, filter_time); }

        // 以下两个不在接口里，保留以备以后使用
        public short SetEquiv(ushort card, ushort axis, double equiv) { return LTDMC.dmc_set_equiv(card, axis, equiv); }
        public short GetEquiv(ushort card, ushort axis, ref double equiv) { return LTDMC.dmc_get_equiv(card, axis, ref equiv); }
    }
}

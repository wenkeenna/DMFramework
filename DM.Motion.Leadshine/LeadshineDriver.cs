using DM.Core.Common;
using DM.Core.Logging;
using DM.Motion.Leadshine.Drivers;
using DM.Motion.Models;
using DM.Motion.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Leadshine
{
    /// <summary>
    /// 雷赛运动卡驱动。只做"下发命令 + 读状态"，所有方法立即返回；等待、取消、软限位、互斥都在 MotionService。
    ///
    /// 回零是一个多步过程（回零 → 偏移 → 位置置零），驱动内部用小状态机处理：
    /// 每次 GetState 发现"当前阶段已完成"就推进到下一阶段，所以对 MotionService 来说回零和普通运动一样。
    /// 刚下发命令的 20ms 内一律报告"运动中"，防止硬件状态还没刷新就被误判为已完成。
    /// 线程安全：状态机用 _g 保护；Stop 可在任意线程调用（取消、急停都会直接调它）。
    /// </summary>
    public sealed class LeadshineDriver : IMotionDriver, IMotionDriverExtras
    {
        private const double MinMoveMs = 20;

        private enum Phase { Idle, Homing, HomeOffset }

        private sealed class Rt
        {
            public Phase Phase;
            public bool Homed;
            public bool Line2;          // 轴正处于直线插补中，停止时要连坐标系一起停
            public ushort Crd;
            public long Started;
        }

        private readonly ILtdmcApi _api;
        private readonly LeadshineOptions _opt;
        private readonly AppPaths _paths;
        private readonly IAppLogger _log;
        private readonly object _g = new object();
        private readonly Dictionary<string, Rt> _rt = new Dictionary<string, Rt>(StringComparer.OrdinalIgnoreCase);

        public LeadshineDriver(ILtdmcApi api, LeadshineOptions options, AppPaths paths, IAppLogger log)
        { _api = api; _opt = options; _paths = paths; _log = log; }

        // ───────────── 连接 ─────────────
        public void Open(MotionConfig cfg)
        {
            string err = _opt.Check();
            if (err != null) throw new InvalidOperationException("Leadshine 配置错误：" + err);

            short boards = _api.BoardInit();
            if (boards <= 0) throw new InvalidOperationException("雷赛控制卡初始化失败（未找到控制卡或驱动未安装），返回值：" + boards);
            _log.Info("雷赛控制卡初始化成功，卡数：" + boards);

            var cards = new SortedSet<int>(cfg.Axes.Select(a => a.Card)
                .Concat(cfg.Inputs.Select(i => i.Card)).Concat(cfg.Outputs.Select(o => o.Card)).Concat(_opt.CardConfigFiles.Keys));

            foreach (int card in cards)
            {
                string path;
                if (!_opt.CardConfigFiles.TryGetValue(card, out path) || string.IsNullOrWhiteSpace(path))
                    throw new InvalidOperationException("未配置卡 " + card + " 的配置文件（machine.json 的 Leadshine.CardConfigFiles）。");

                path = Path.IsPathRooted(path) ? path : Path.Combine(_paths.Config, path);
                if (!File.Exists(path)) throw new FileNotFoundException("卡 " + card + " 的配置文件不存在：" + path, path);

                short ret = _api.DownloadConfigFile((ushort)card, path);
                if (ret != 0) throw new InvalidOperationException("卡 " + card + " 下载配置文件失败，返回值 " + ret + "：" + path);
                _log.Info("雷赛卡 " + card + " 已下载配置：" + path);
            }

            lock (_g)
            {
                _rt.Clear();
                foreach (AxisConfig a in cfg.Axes) _rt[a.Name] = new Rt();
            }

            if (_opt.AutoEnable)
                foreach (AxisConfig a in cfg.Axes)
                {
                    try { SetEnable(a, true); }
                    catch (Exception ex) { _log.Warn("轴 " + a.Name + " 上使能失败：" + ex.Message); }
                }
        }

        /// <summary>
        /// 不下使能：竖直轴下使能会掉落。进程退出时由操作系统回收控制卡资源。
        /// 如果 ILtdmcApi 里有 dmc_board_close 的封装，可以在这里调用一次。
        /// </summary>
        public void Close() { }

        // ───────────── 运动 ─────────────
        public void StartMove(AxisConfig a, double targetPulse, bool relative, MoveParams p)
        {
            EnsureNotAlarmed(a, "运动");
            ApplyProfile(a, p.VelocityPulse, p.AccTime, p.DecTime);
            int pulses = ToPulse(a, targetPulse);
            Begin(a);

            ushort card = (ushort)a.Card, ax = (ushort)a.Index;
            short r = relative ? _api.PMove(card, ax, pulses, 0) : _api.PMove(card, ax, pulses, 1);
            Ensure(r, a, relative ? "相对运动指令失败" : "绝对运动指令失败");
        }

        public void StartLine2(AxisConfig a, AxisConfig b, double targetA, double targetB, MoveParams p)
        {
            if (a.Card != b.Card)
                throw new InvalidOperationException("双轴插补要求同一控制卡：" + a.Name + "(卡 " + a.Card + ")，" + b.Name + "(卡 " + b.Card + ")。");
            EnsureNotAlarmed(a, "双轴直线插补");
            EnsureNotAlarmed(b, "双轴直线插补");

            ushort card = (ushort)a.Card, crd = (ushort)_opt.LineCoordinate;
            double v = p.VelocityPulse, min = Math.Min(v, Math.Max(1.0, v * 0.1));
            double acc = p.AccTime > 0 ? p.AccTime : 0.3, dec = p.DecTime > 0 ? p.DecTime : 0.3;

            short ret = _api.SetVectorProfileMulticoor(card, crd, min, v, acc, dec, min);
            if (ret != 0) _log.Warn("设置插补速度曲线返回 " + ret + "（旧版同样不处理该返回值）。");

            ushort[] axisList = { (ushort)a.Index, (ushort)b.Index };
            int[] target = { ToPulse(a, targetA), ToPulse(b, targetB) };
            ushort posiMode = 1;                                    // 绝对坐标

            Begin(a); Begin(b);
            ret = _api.LineMulticoor(card, crd, 2, axisList, target, posiMode);
            Ensure(ret, a, "下发直线插补指令失败");

            lock (_g) { Rt ra = Get(a), rb = Get(b); ra.Line2 = rb.Line2 = true; ra.Crd = rb.Crd = crd; }
        }

        public void StartHome(AxisConfig a)
        {
            EnsureNotAlarmed(a, "回零");
            ApplyProfile(a, a.HomeVelocity * a.PulsePerUnit, a.AccTime, a.DecTime);
            Begin(a);
            lock (_g) { Rt rt = Get(a); rt.Phase = Phase.Homing; rt.Homed = false; }

            Ensure(_api.HomeMove((ushort)a.Card, (ushort)a.Index), a, "回零指令失败");
        }

        /// <summary>
        /// 停止。emergency=true 立即停止，false 减速停止（雷赛 dmc_stop 约定：1 立即、0 减速；
        /// 请核对旧 StopMode 枚举的数值与此一致）。轴在直线插补中时同时停插补坐标系。
        /// </summary>
        public void Stop(AxisConfig a, bool emergency)
        {
            ushort mode = (ushort)(emergency ? 1 : 0);
            bool line2; ushort crd;
            lock (_g)
            {
                Rt rt = Get(a);
                if (rt.Phase != Phase.Idle) { rt.Phase = Phase.Idle; rt.Homed = false; }     // 回零被打断，原点作废
                line2 = rt.Line2; crd = rt.Crd; rt.Line2 = false;
            }

            short r2 = line2 ? _api.StopMulticoor((ushort)a.Card, crd, mode) : (short)0;
            short r1 = _api.Stop((ushort)a.Card, (ushort)a.Index, mode);
            if (r1 != 0 || r2 != 0) throw new InvalidOperationException("轴 " + a.Name + " 停止指令失败，错误码：" + (r1 != 0 ? r1 : r2));
        }

        public AxisState GetState(AxisConfig a)
        {
            ushort card = (ushort)a.Card, ax = (ushort)a.Index;
            lock (_g)
            {
                Rt rt = Get(a);
                bool alarm = (_api.GetAxisIoStatus(card, ax) & 1) != 0;
                bool done = _api.CheckDone(card, ax) == 1 && ElapsedMs(rt) >= MinMoveMs;
                bool moving = !done;

                if (rt.Phase != Phase.Idle)
                {
                    if (alarm) { rt.Phase = Phase.Idle; rt.Homed = false; moving = false; }                  // 回零中报警：中止，由服务抛出报警
                    else if (done)
                    {
                        if (rt.Phase == Phase.Homing && Math.Abs(a.HomeOffset) > 1e-9) { StartHomeOffset(a, rt); moving = true; }
                        else { FinishHome(a, rt); moving = false; }
                    }
                }
                else if (done) rt.Line2 = false;

                return new AxisState
                {
                    PositionPulse = Convert.ToDouble(_api.GetPosition(card, ax)),
                    IsMoving = moving,
                    Alarm = alarm,
                    Homed = rt.Homed
                };
            }
        }

        private void StartHomeOffset(AxisConfig a, Rt rt)
        {
            ApplyProfile(a, a.HomeVelocity * a.PulsePerUnit, a.AccTime, a.DecTime);
            Ensure(_api.PMove((ushort)a.Card, (ushort)a.Index, ToPulse(a, a.HomeOffset * a.PulsePerUnit), 0), a, "回零偏移指令失败");
            rt.Phase = Phase.HomeOffset;
            rt.Started = Stopwatch.GetTimestamp();
        }

        private void FinishHome(AxisConfig a, Rt rt)
        {
            rt.Phase = Phase.Idle;
            Ensure(_api.SetPosition((ushort)a.Card, (ushort)a.Index, 0), a, "置零位置失败");
            rt.Homed = true;
        }

        // ───────────── IO ─────────────
        public bool ReadInput(IoConfig io) { return _api.ReadInBit((ushort)io.Card, (ushort)io.Bit) != 0; }
        public bool ReadOutput(IoConfig io) { return _api.ReadOutBit((ushort)io.Card, (ushort)io.Bit) != 0; }

        public void WriteOutput(IoConfig io, bool level)
        {
            ushort v = (ushort)(level ? 1 : 0);
            short r = _api.WriteOutBit((ushort)io.Card, (ushort)io.Bit, v);
            if (r != 0) throw new InvalidOperationException("写输出失败：" + io.Name + "（卡 " + io.Card + "，位 " + io.Bit + "），错误码：" + r);
        }

        // ───────────── 可选能力：点动、使能、清报警 ─────────────
        public void StartJog(AxisConfig a, bool positive, MoveParams p)
        {
            EnsureNotAlarmed(a, "点动");
            ApplyProfile(a, p.VelocityPulse, p.AccTime, p.DecTime);
            Begin(a);
            ushort card = (ushort)a.Card, ax = (ushort)a.Index;
            short r = positive ? _api.VMove(card, ax, 1) : _api.VMove(card, ax, 0);
            Ensure(r, a, "点动指令失败");
        }

        public void SetEnable(AxisConfig a, bool enable)
        {
            ushort card = (ushort)a.Card, ax = (ushort)a.Index;
            short r = enable ? _api.WriteSevonPin(card, ax, 0) : _api.WriteSevonPin(card, ax, 1);      // 低电平使能
            Ensure(r, a, enable ? "上使能失败" : "下使能失败");
        }

        /// <summary>清报警：关使能 → ERC 高 → ERC 低 → 重新使能（沿用旧版时序）。竖直轴关使能会掉落。</summary>
        public async Task ClearAlarmAsync(AxisConfig a, CancellationToken ct)
        {
            ushort card = (ushort)a.Card, ax = (ushort)a.Index;
            Ensure(_api.WriteSevonPin(card, ax, 1), a, "关闭使能失败");
            await Task.Delay(100, ct);
            Ensure(_api.WriteErcPin(card, ax, 1), a, "拉高 ERC 失败");
            await Task.Delay(100, ct);
            Ensure(_api.WriteErcPin(card, ax, 0), a, "拉低 ERC 失败");
            await Task.Delay(100, ct);
            Ensure(_api.WriteSevonPin(card, ax, 0), a, "重新使能失败");
        }

        // ───────────── 内部 ─────────────
        /// <summary>起始/结束速度 = 最高速 × StartVelocityRatio，S 曲线参数 0,0，与旧版一致。加减速参数是时间（秒）。</summary>
        private void ApplyProfile(AxisConfig a, double velocityPulse, double accSec, double decSec)
        {
            ushort card = (ushort)a.Card, ax = (ushort)a.Index;
            double min = velocityPulse * _opt.StartVelocityRatio;
            Ensure(_api.SetProfile(card, ax, min, velocityPulse, accSec, decSec, min), a, "设置速度曲线失败");
            Ensure(_api.SetSProfile(card, ax, 0, 0), a, "设置 S 曲线失败");
        }

        private void EnsureNotAlarmed(AxisConfig a, string action)
        {
            if ((_api.GetAxisIoStatus((ushort)a.Card, (ushort)a.Index) & 1) != 0)
                throw new InvalidOperationException("轴 " + a.Name + " 当前处于报警状态，禁止执行" + action + "。请先清报警后再试。");
        }

        private void Begin(AxisConfig a)
        {
            lock (_g) { Rt rt = Get(a); rt.Phase = Phase.Idle; rt.Line2 = false; rt.Started = Stopwatch.GetTimestamp(); }
        }

        private Rt Get(AxisConfig a)
        {
            Rt r;
            if (!_rt.TryGetValue(a.Name, out r)) throw new InvalidOperationException("驱动未初始化轴：" + a.Name);
            return r;
        }

        private static double ElapsedMs(Rt rt) { return (Stopwatch.GetTimestamp() - rt.Started) * 1000.0 / Stopwatch.Frequency; }

        private static int ToPulse(AxisConfig a, double pulses)
        {
            double r = Math.Round(pulses);
            if (r > int.MaxValue || r < int.MinValue) throw new InvalidOperationException("轴 " + a.Name + " 目标脉冲数超出范围：" + pulses);
            return (int)r;
        }

        private static void Ensure(short r, AxisConfig a, string message)
        { if (r != 0) throw new InvalidOperationException("轴 " + a.Name + "：" + message + "，错误码：" + r); }
    }
}

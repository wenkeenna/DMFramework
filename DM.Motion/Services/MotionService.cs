using DM.Core.Common;
using DM.Core.Devices;
using DM.Core.Events;
using DM.Core.Logging;
using DM.Motion.Models;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Services
{
    /// <summary>
    /// 要点：
    /// 1. 同时是 IDeviceSession（设备名 MotionCard）：连接/断开由设备中心后台处理。
    /// 2. 未连接时所有硬件调用抛明确错误，不会空引用。
    /// 3. 取消 = 立即停轴：ct.Register 里直接调驱动 Stop，不等 await 回到本线程。
    /// 4. 急停：订阅急停事件直接停所有轴，与脚本、运行器是否响应无关。
    /// 5. 同轴互斥：运动期间再次对同一根轴下发运动会被拒绝。
    /// </summary>
    public sealed class MotionService : IMotionService, IDeviceSession
    {
        public const string DeviceName = "MotionCard";

        private readonly MotionConfig _cfg;
        private readonly IMotionDriverCatalog _catalog;
        private readonly IDeviceHub _hub;
        private readonly IAppLogger _log;
        private readonly Dictionary<string, AxisConfig> _axes = new Dictionary<string, AxisConfig>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IoConfig> _in = new Dictionary<string, IoConfig>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IoConfig> _out = new Dictionary<string, IoConfig>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _busy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private volatile IMotionDriver _driver;

        public MotionService(IMachineConfig machine, IMotionDriverCatalog catalog, IDeviceHub hub,
                             IEventAggregator events, IAppLogger log)
        {
            _cfg = machine.Get<MotionConfig>("Motion");
            _catalog = catalog; _hub = hub; _log = log;
            foreach (var a in _cfg.Axes) if (!string.IsNullOrWhiteSpace(a.Name)) _axes[a.Name] = a;
            foreach (var i in _cfg.Inputs) if (!string.IsNullOrWhiteSpace(i.Name)) _in[i.Name] = i;
            foreach (var o in _cfg.Outputs) if (!string.IsNullOrWhiteSpace(o.Name)) _out[o.Name] = o;

            events.GetEvent<EmergencyStopEvent>().Subscribe(m => { if (m != null && m.IsActive) StopAll(); },
                                                            ThreadOption.PublisherThread);
        }

        public IReadOnlyList<AxisConfig> Axes { get { return _cfg.Axes; } }
        public IReadOnlyList<IoConfig> Inputs { get { return _cfg.Inputs; } }
        public IReadOnlyList<IoConfig> Outputs { get { return _cfg.Outputs; } }

        // ───── IDeviceSession ─────
        public string Name { get { return DeviceName; } }

        public Task ConnectAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                List<string> errors = _cfg.Validate();
                if (errors.Count > 0) throw new InvalidOperationException("运动配置错误：\n" + string.Join("\n", errors));
                IMotionDriver d = _catalog.Create(_cfg.Driver);
                d.Open(_cfg);
                _driver = d;
            }, ct);
        }

        public Task DisconnectAsync()
        {
            return Task.Run(() =>
            {
                IMotionDriver d = _driver;
                if (d == null) return;
                StopAll();
                _driver = null;
                d.Close();
            });
        }

        // ───── 运动 ─────
        public async Task MoveAsync(string axis, double position, bool relative, double? velocity, CancellationToken ct)
        {
            AxisConfig a = Axis(axis);
            IMotionDriver d = Ready();

            double vel = velocity ?? a.DefaultVelocity;
            if (vel <= 0 || vel > a.MaxVelocity)
                throw new InvalidOperationException("轴 " + a.Name + " 速度 " + vel + " 超出范围 (0, " + a.MaxVelocity + "]。");

            double target = relative ? PosUnits(d, a) + position : position;
            if ((a.MinPos.HasValue && target < a.MinPos.Value) || (a.MaxPos.HasValue && target > a.MaxPos.Value))
                throw new InvalidOperationException("轴 " + a.Name + " 目标位置 " + target + " 超出软限位 [" + a.MinPos + ", " + a.MaxPos + "]。");

            using (Lock(a.Name))
            {
                ct.ThrowIfCancellationRequested();
                d.StartMove(a, position * a.PulsePerUnit, relative,
                    new MoveParams { VelocityPulse = vel * a.PulsePerUnit, AccTime = a.AccTime, DecTime = a.DecTime });

                using (ct.Register(() => SafeStop(d, a, true)))        // 取消 → 立即停轴
                {
                    await WaitDoneAsync(d, a, ct);
                }

                double err = Math.Abs(PosUnits(d, a) - target);
                if (err > a.InPosTolerance)
                    throw new InvalidOperationException("轴 " + a.Name + " 未到位：目标 " + target + "，偏差 " + err + "。");
            }
        }

        public async Task HomeAsync(string axis, CancellationToken ct)
        {
            AxisConfig a = Axis(axis);
            IMotionDriver d = Ready();
            using (Lock(a.Name))
            {
                ct.ThrowIfCancellationRequested();
                d.StartHome(a);
                using (ct.Register(() => SafeStop(d, a, true)))
                {
                    await WaitDoneAsync(d, a, ct);
                }
                if (!d.GetState(a).Homed) throw new InvalidOperationException("轴 " + a.Name + " 回零失败。");
            }
        }

        private static async Task WaitDoneAsync(IMotionDriver d, AxisConfig a, CancellationToken ct)
        {
            await Task.Delay(5, ct);              // 给硬件一点启动时间，避免刚下发就读到"已停止"
            while (true)
            {
                AxisState s = d.GetState(a);
                if (s.Alarm) throw new InvalidOperationException("轴 " + a.Name + " 报警。");
                if (!s.IsMoving) return;
                await Task.Delay(10, ct);
            }
        }

        public void Stop(string axis)
        {
            IMotionDriver d = _driver;
            if (d != null) SafeStop(d, Axis(axis), false);
        }

        public void StopAll()
        {
            IMotionDriver d = _driver;
            if (d == null) return;
            foreach (AxisConfig a in _axes.Values) SafeStop(d, a, true);
        }

        private void SafeStop(IMotionDriver d, AxisConfig a, bool emergency)
        {
            try { d.Stop(a, emergency); }
            catch (Exception ex) { _log.Error("停止轴失败：" + a.Name, ex); }    // 一根失败不影响其他
        }

        public double GetPosition(string axis) { AxisConfig a = Axis(axis); return PosUnits(Ready(), a); }
        private static double PosUnits(IMotionDriver d, AxisConfig a) { return d.GetState(a).PositionPulse / a.PulsePerUnit; }

        // ───── IO ─────
        public bool GetInput(string name) { IoConfig io = In(name); return Ready().ReadInput(io) ^ io.Inverted; }
        public bool GetOutput(string name) { IoConfig io = Out(name); return Ready().ReadOutput(io) ^ io.Inverted; }
        public void SetOutput(string name, bool on) { IoConfig io = Out(name); Ready().WriteOutput(io, on ^ io.Inverted); }

        public async Task WaitInputAsync(string name, bool expected, int timeoutMs, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            while (GetInput(name) != expected)
            {
                if (timeoutMs > 0 && sw.ElapsedMilliseconds >= timeoutMs)
                    throw new TimeoutException("等待输入 " + name + " = " + (expected ? 1 : 0) + " 超时（" + timeoutMs + "ms）。");
                await Task.Delay(10, ct);
            }
        }

        // ───── 内部 ─────
        private IMotionDriver Ready()
        {
            DeviceState s = _hub.GetState(DeviceName);
            IMotionDriver d = _driver;
            if (s != DeviceState.Connected || d == null)
                throw new InvalidOperationException("运动控制卡未连接（当前状态：" + s + "）。");
            return d;
        }

        private AxisConfig Axis(string n)
        { AxisConfig a; if (!_axes.TryGetValue(n ?? "", out a)) throw new InvalidOperationException("未定义的轴：" + n); return a; }
        private IoConfig In(string n)
        { IoConfig io; if (!_in.TryGetValue(n ?? "", out io)) throw new InvalidOperationException("未定义的输入：" + n); return io; }
        private IoConfig Out(string n)
        { IoConfig io; if (!_out.TryGetValue(n ?? "", out io)) throw new InvalidOperationException("未定义的输出：" + n); return io; }

        private IDisposable Lock(string axis)
        {
            lock (_busy) { if (!_busy.Add(axis)) throw new InvalidOperationException("轴 " + axis + " 正在运动，请先停止。"); }
            return new Releaser(() => { lock (_busy) _busy.Remove(axis); });
        }

        private sealed class Releaser : IDisposable
        {
            private Action _a;
            public Releaser(Action a) { _a = a; }
            public void Dispose() { Action a = Interlocked.Exchange(ref _a, null); if (a != null) a(); }
        }
    }
}

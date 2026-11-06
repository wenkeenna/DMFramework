using DM.Motion.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Services
{
    /// <summary>
    /// 驱动只做"下发命令 + 读状态"，全部同步、立即返回。
    /// 等待到位、超时、取消、软限位、到位判断都在 MotionService，驱动里不要写。
    /// Stop 必须能在任意线程调用且立即生效（急停和取消都会直接调用它）。
    /// </summary>
    public interface IMotionDriver
    {
        void Open(MotionConfig cfg);                    // 可能很慢，由设备中心放在线程池里调用
        void Close();
        void StartMove(AxisConfig a, double targetPulse, bool relative, MoveParams p);
        /// <summary>两轴直线插补，目标为绝对脉冲，p.VelocityPulse 是合成速度（脉冲/秒）。</summary>
        void StartLine2(AxisConfig a, AxisConfig b, double targetPulseA, double targetPulseB, MoveParams p);
        void StartHome(AxisConfig a);
        void Stop(AxisConfig a, bool emergency);
        AxisState GetState(AxisConfig a);
        bool ReadInput(IoConfig io);                    // 原始电平，反相由服务处理
        bool ReadOutput(IoConfig io);
        void WriteOutput(IoConfig io, bool level);
    }

    public interface IMotionDriverCatalog
    {
        void Add(string name, Func<IMotionDriver> factory);
        IMotionDriver Create(string name);
    }

    public sealed class MotionDriverCatalog : IMotionDriverCatalog
    {
        private readonly Dictionary<string, Func<IMotionDriver>> _m = new Dictionary<string, Func<IMotionDriver>>(StringComparer.OrdinalIgnoreCase);

        public void Add(string name, Func<IMotionDriver> factory)
        {
            if (_m.ContainsKey(name)) throw new InvalidOperationException("运动驱动重名：" + name);
            _m[name] = factory;
        }

        public IMotionDriver Create(string name)
        {
            Func<IMotionDriver> f;
            if (!_m.TryGetValue(name ?? "", out f))
                throw new InvalidOperationException("未注册的运动驱动：" + name + "（已注册：" + (_m.Count == 0 ? "无" : string.Join("、", _m.Keys)) + "）");
            return f();
        }
    }

    public interface IMotionService
    {
        bool IsConnected { get; }
        IReadOnlyList<AxisConfig> Axes { get; }
        IReadOnlyList<IoConfig> Inputs { get; }
        IReadOnlyList<IoConfig> Outputs { get; }

        /// <summary>位置单位与 machine.json 一致。令牌取消时立即停轴并抛 OperationCanceledException。</summary>
        Task MoveAsync(string axis, double position, bool relative, double? velocity, CancellationToken ct);
        Task Line2Async(string axisA, double posA, string axisB, double posB, double velocity, CancellationToken ct);
        Task HomeAsync(string axis, CancellationToken ct);
        void Stop(string axis);
        void StopAll();
        double GetPosition(string axis);
        AxisStatus GetStatus(string axis);

        bool GetInput(string name);
        bool GetOutput(string name);
        void SetOutput(string name, bool on);
        Task WaitInputAsync(string name, bool expected, int timeoutMs, CancellationToken ct);   // 0=一直等；超时抛 TimeoutException
    }

    /// <summary>驱动的可选能力：点动、清报警、使能。MotionService 不依赖它，手动运动面板按需使用。</summary>
    public interface IMotionDriverExtras
    {
        void StartJog(AxisConfig a, bool positive, MoveParams p);       // 停止用 IMotionDriver.Stop
        Task ClearAlarmAsync(AxisConfig a, CancellationToken ct);       // 会先关使能，竖直轴会掉落，界面上要有确认
        void SetEnable(AxisConfig a, bool enable);
    }


}

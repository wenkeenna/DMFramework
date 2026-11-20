using DM.Core.Actions;
using DM.Core.Audit;
using DM.Core.Identity;
using DM.Engine.Common;
using System.Collections.Generic;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace DM.Motion.Services
{
    /// <summary>
    /// 手动运动界面只调用这个类：权限检查、联锁、审计自动生效，和自动运行走同一个 IMotionService。
    /// 停止不经过动作网关：急停状态下"停"必须始终可用，不能被联锁挡住。
    /// </summary>
    public sealed class ManualMotionService
    {
        public static readonly AccessRole MoveRole = AccessRole.Operator;
        public static readonly AccessRole OutputRole = AccessRole.Engineer;      // 强制输出风险较高
        public static readonly AccessRole EngineerRole = AccessRole.Engineer;
        private readonly IMotionService _motion;
        private readonly IActionGateway _gate;
        private readonly IAuditService _audit;

        public ManualMotionService(IMotionService motion, IActionGateway gate, IAuditService audit)
        { 
            _motion = motion; 
            _gate = gate; 
            _audit = audit; 
        }

        public Task MoveAsync(string axis, double pos, bool relative, CancellationToken ct)
        {
            return _gate.RunManualAsync(relative ? "MoveRel" : "MoveAbs", axis, MoveRole,
                () => _motion.MoveAsync(axis, pos, relative, null, ct), pos.ToString(CultureInfo.InvariantCulture));
        }

        public Task HomeAsync(string axis, CancellationToken ct)
        { 
            return _gate.RunManualAsync("Home", axis, MoveRole, () => _motion.HomeAsync(axis, ct)); 
        }

        /// <summary>点动一步：相对运动 ±step，速度超过轴最高速时按最高速。</summary>
        public Task StepAsync(string axis, bool positive, double step, double speed, CancellationToken ct)
        {
            return _gate.RunManualAsync("Step", axis, MoveRole,
                () => _motion.MoveAsync(axis, positive ? step : -step, true, Speed(axis, speed), ct),
                (positive ? "+" : "-") + step.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>两根轴同步点动一步（左右 FA 同步模式）。</summary>
        public Task StepPairAsync(string a, string b, bool positive, double step, double speed, CancellationToken ct)
        {
            double d = positive ? step : -step;
            var work = new List<Func<CancellationToken, Task>>
            {
                t => _motion.MoveAsync(a, d, true, Speed(a, speed), t),
                t => _motion.MoveAsync(b, d, true, Speed(b, speed), t)
            };
            return _gate.RunManualAsync("StepPair", a + "," + b, MoveRole, () => ParallelRun.AllAsync(work, ct), d.ToString(CultureInfo.InvariantCulture));
        }

        public Task JogStartAsync(string axis, bool positive, double speed)
        {
            return _gate.RunManualAsync("Jog", axis, MoveRole,
                () => { _motion.JogStart(axis, positive, Speed(axis, speed) ?? 0); return Task.CompletedTask; }, positive ? "+" : "-");
        }

        public void JogStop(string axis) { _motion.JogStop(axis); }

        public Task ClearAlarmAsync(string axis, CancellationToken ct)
        { return _gate.RunManualAsync("ClearAlarm", axis, EngineerRole, () => _motion.ClearAlarmAsync(axis, ct)); }




        public Task SetOutputAsync(string name, bool on)
        {
            return _gate.RunManualAsync("SetOut", name, OutputRole,
                () => { _motion.SetOutput(name, on); return Task.CompletedTask; }, on ? "1" : "0");
        }

        public void Stop(string axis) { _motion.Stop(axis); _audit.Record(AuditCategory.Operation, "Stop", axis); }
        public void StopAll() { _motion.StopAll(); _audit.Record(AuditCategory.Operation, "StopAll", "*"); }

        private double? Speed(string axis, double speed)
        {
            if (speed <= 0) return null;
            var a = _motion.Axes.FirstOrDefault(x => string.Equals(x.Name, axis, StringComparison.OrdinalIgnoreCase));
            return a == null ? speed : Math.Min(speed, a.MaxVelocity);
        }

    }
}

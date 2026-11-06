using DM.Core.Actions;
using DM.Core.Audit;
using DM.Core.Identity;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

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

        private readonly IMotionService _motion;
        private readonly IActionGateway _gate;
        private readonly IAuditService _audit;

        public ManualMotionService(IMotionService motion, IActionGateway gate, IAuditService audit)
        { _motion = motion; _gate = gate; _audit = audit; }

        public Task MoveAsync(string axis, double pos, bool relative, CancellationToken ct)
        {
            return _gate.RunManualAsync(relative ? "MoveRel" : "MoveAbs", axis, MoveRole,
                () => _motion.MoveAsync(axis, pos, relative, null, ct), pos.ToString(CultureInfo.InvariantCulture));
        }

        public Task HomeAsync(string axis, CancellationToken ct)
        { return _gate.RunManualAsync("Home", axis, MoveRole, () => _motion.HomeAsync(axis, ct)); }

        public Task SetOutputAsync(string name, bool on)
        {
            return _gate.RunManualAsync("SetOut", name, OutputRole,
                () => { _motion.SetOutput(name, on); return Task.CompletedTask; }, on ? "1" : "0");
        }

        public void Stop(string axis) { _motion.Stop(axis); _audit.Record(AuditCategory.Operation, "Stop", axis); }
        public void StopAll() { _motion.StopAll(); _audit.Record(AuditCategory.Operation, "StopAll", "*"); }
    }
}

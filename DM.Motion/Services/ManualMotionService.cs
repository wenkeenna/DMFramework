using DM.Core.Actions;
using DM.Core.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Services
{
    /// <summary>手动运动界面只调用这个类，权限和审计自动生效。</summary>
    public sealed class ManualMotionService
    {
        private readonly IMotionService _motion;
        private readonly IActionGateway _gate;
        public ManualMotionService(IMotionService motion, IActionGateway gate) { _motion = motion; _gate = gate; }

        public Task MoveAsync(string axis, double pos, bool relative, CancellationToken ct)
        {
            return _gate.RunManualAsync(relative ? "MoveRel" : "MoveAbs", axis, AccessRole.Operator,
                () => _motion.MoveAsync(axis, pos, relative, null, ct), pos.ToString());
        }

        public Task HomeAsync(string axis, CancellationToken ct)
        { return _gate.RunManualAsync("Home", axis, AccessRole.Operator, () => _motion.HomeAsync(axis, ct)); }

        public Task SetOutputAsync(string name, bool on)
        {
            // 强制输出风险较高，要求工程师以上
            return _gate.RunManualAsync("SetOut", name, AccessRole.Engineer,
                () => { _motion.SetOutput(name, on); return Task.CompletedTask; }, on ? "1" : "0");
        }
    }
}

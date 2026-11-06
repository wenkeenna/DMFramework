using DM.Core.Actions;
using DM.Motion.Services;
using DM.Process.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Process.Services
{
    public sealed class ProbeService : IProbeService
    {
        private readonly IMotionService _motion;
        private readonly ForceSourceProvider _force;
        private readonly IActionGateway _gate;

        public ProbeService(IMotionService motion, ForceSourceProvider force, IActionGateway gate)
        { _motion = motion; _force = force; _gate = gate; }

        public async Task<double> ProbeAsync(ProbeParams p, Action<string> log, CancellationToken ct)
        {
            double contact = 0;
            await _gate.RunAutoAsync("PROBE", p.Axis, async () => { contact = await CoreAsync(p, log ?? (m => { }), ct); });
            return contact;
        }

        private async Task<double> CoreAsync(ProbeParams p, Action<string> log, CancellationToken ct)
        {
            string err = p.Check();
            if (err != null) throw new InvalidOperationException(err);
            IForceSource force = _force.Get();
            if (!force.IsConnected) throw new InvalidOperationException("力传感器未连接。");

            double sign = p.Direction == ProbeDirection.Positive ? 1 : -1, moved = 0;
            log("探测开始：轴 " + p.Axis + "，通道 " + p.Channel + "，触发力 " + p.TriggerForce.ToString(CultureInfo.InvariantCulture));

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (moved + p.StepDistance > p.MaxTravel + 1e-9)
                    throw new InvalidOperationException("探测超过最大行程 " + p.MaxTravel + "，仍未达到触发力 " + p.TriggerForce + "。");

                await _motion.MoveAsync(p.Axis, sign * p.StepDistance, true, p.StepVelocity, ct);       // 取消时服务会立即停轴
                moved += p.StepDistance;
                if (p.SettleMs > 0) await Task.Delay(p.SettleMs, ct);

                double f = force.Read(p.Channel);
                log("探测 力=" + f.ToString("F4", CultureInfo.InvariantCulture) + " 位置=" + _motion.GetPosition(p.Axis).ToString("F4", CultureInfo.InvariantCulture));
                if (Math.Abs(f) < p.TriggerForce) continue;

                double contact = _motion.GetPosition(p.Axis);
                if (p.Retreat == RetreatMode.Absolute) await _motion.MoveAsync(p.Axis, p.RetreatValue, false, p.RetreatVelocity, ct);
                else await _motion.MoveAsync(p.Axis, -sign * p.RetreatValue, true, p.RetreatVelocity, ct);
                log("探测完成，接触位置 " + contact.ToString("F4", CultureInfo.InvariantCulture));
                return contact;
            }
        }
    }
}

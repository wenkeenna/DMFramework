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
    public sealed class GlueService : IGlueService
    {
        private readonly IMotionService _motion;
        private readonly IProbeService _probe;
        private readonly IActionGateway _gate;

        public GlueService(IMotionService motion, IProbeService probe, IActionGateway gate)
        { _motion = motion; _probe = probe; _gate = gate; }

        public Task RunAsync(GlueJob j, Action<string> log, CancellationToken ct)
        { return _gate.RunAutoAsync("GLUE", j.Mode.ToString(), () => CoreAsync(j, log ?? (m => { }), ct)); }

        /// <summary>
        /// 安全规则：无论成功、失败还是取消，点胶输出一定关闭（先置标志再开输出，开输出中途失败也会去关）。
        /// 失败或取消时不收气缸、不抬 Z：此时 Z 可能在下降位，收回可能撞件，交给操作员处理。
        /// </summary>
        private async Task CoreAsync(GlueJob j, Action<string> log, CancellationToken ct)
        {
            GlueSettings s = j.Settings;
            string err = s.Check();
            if (err != null) throw new InvalidOperationException(err);

            double targetZ = s.ZPos + j.CalibZ, safeZ = targetZ - s.ZSafeOffset;
            bool glueOn = false;
            log("点胶开始：" + j.Mode + "，Z=" + targetZ.ToString("F4", CultureInfo.InvariantCulture));

            try
            {
                await _motion.MoveAsync(s.AxisX, j.X1, false, s.MoveSpeed, ct);          // 保持旧路径：先 X 后 Y
                await _motion.MoveAsync(s.AxisY, j.Y1, false, s.MoveSpeed, ct);

                _motion.SetOutput(s.CylinderOutput, true);
                await _motion.WaitInputAsync(s.CylinderForwardInput, true, s.CylinderTimeoutMs, ct);

                await _motion.MoveAsync(s.AxisZ, safeZ, false, s.MoveSpeed, ct);
                await _motion.MoveAsync(s.AxisZ, targetZ, false, s.MoveSpeed, ct);

                if (s.Touch.IsEnabled) await _probe.ProbeAsync(s.Touch.ToProbe(s.AxisZ), log, ct);

                glueOn = true;
                _motion.SetOutput(s.GlueOutput, true);

                if (j.Mode == GlueMode.Point) await Task.Delay(s.GlueTimeMs, ct);
                else if (j.Direction == GlueLineDirection.X) await _motion.MoveAsync(s.AxisX, j.X2, false, s.LineSpeed, ct);
                else await _motion.MoveAsync(s.AxisY, j.Y2, false, s.LineSpeed, ct);

                _motion.SetOutput(s.GlueOutput, false);
                glueOn = false;

                await _motion.MoveAsync(s.AxisZ, safeZ, false, s.MoveSpeed, ct);
                if (!s.KeepCylinderExtended)
                {
                    _motion.SetOutput(s.CylinderOutput, false);
                    await _motion.WaitInputAsync(s.CylinderBackInput, true, s.CylinderTimeoutMs, ct);
                }
                log("点胶完成。");
            }
            finally
            {
                if (glueOn)
                {
                    try { _motion.SetOutput(s.GlueOutput, false); log("已关闭点胶输出。"); }
                    catch (Exception ex) { log("【严重】关闭点胶输出失败，请立即人工处理：" + ex.Message); }
                }
            }
        }
    }
}

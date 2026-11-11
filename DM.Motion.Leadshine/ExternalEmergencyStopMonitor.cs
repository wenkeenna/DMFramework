using DM.Core.Actions;
using DM.Core.Audit;
using DM.Core.Devices;
using DM.Core.Events;
using DM.Core.Logging;
using DM.Motion.Services;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Leadshine
{
    /// <summary>
    /// 外部急停按钮监控（设备名 EStopMonitor，只有配置了 EmergencyInput 才注册）。
    /// 1. 电平触发：只要按钮是按下状态且系统还没处于急停，就发布急停事件。
    ///    所以"按住按钮时点解除急停"会在一个轮询周期内被重新触发，物理按钮未复位就无法恢复运行。
    /// 2. 轴的停止不在这里做：运动服务订阅了急停事件，会直接停所有轴；任务运行器、界面也都订阅它。
    /// 3. 按钮松开不会自动解除急停，必须由工程师在界面上手动解除（有审计）。
    /// 4. 运动卡未连接时不监控（没有东西要保护）；连接后读取输入连续失败，按安全侧触发急停。
    /// </summary>
    public sealed class ExternalEmergencyStopMonitor : IDeviceSession
    {
        private const int FailSafeCount = 5;

        private readonly IMotionService _motion;
        private readonly IDeviceHub _hub;
        private readonly ISafetyState _safety;
        private readonly IEventAggregator _events;
        private readonly IAuditService _audit;
        private readonly IAppLogger _log;
        private readonly LeadshineOptions _opt;
        private CancellationTokenSource _cts;
        private Task _loop;

        public ExternalEmergencyStopMonitor(IMotionService motion, IDeviceHub hub, ISafetyState safety,
                                            IEventAggregator events, IAuditService audit, IAppLogger log, LeadshineOptions opt)
        { _motion = motion; _hub = hub; _safety = safety; _events = events; _audit = audit; _log = log; _opt = opt; }

        public string Name => "EStopMonitor";

        public Task ConnectAsync(CancellationToken ct)
        {
            string input = (_opt.EmergencyInput ?? "").Trim();
            if (input.Length == 0) throw new InvalidOperationException("未配置外部急停输入点（Leadshine.EmergencyInput）。");
            if (!_motion.Inputs.Any(i => string.Equals(i.Name, input, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("急停输入点“" + input + "”没有在 Motion.Inputs 中定义。");
            if (_cts != null) return Task.CompletedTask;                    // 幂等

            _cts = new CancellationTokenSource();
            CancellationToken t = _cts.Token;
            _loop = Task.Run(() => LoopAsync(input, t));
            _log.Info("外部急停监控已启动：" + input + "，周期 " + _opt.EmergencyPollMs + "ms");
            return Task.CompletedTask;
        }

        public async Task DisconnectAsync()
        {
            CancellationTokenSource cts = _cts; Task loop = _loop;
            _cts = null; _loop = null;
            if (cts == null) return;
            cts.Cancel();
            if (loop != null) await Task.WhenAny(loop, Task.Delay(1000)).ConfigureAwait(false);
            cts.Dispose();
        }

        private async Task LoopAsync(string input, CancellationToken ct)
        {
            int failures = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_hub.GetState(MotionService.DeviceName) == DeviceState.Connected)
                    {
                        bool pressed = _motion.GetInput(input);
                        failures = 0;
                        if (pressed && !_safety.IsEmergencyStopped) Trigger("外部急停按钮按下");
                    }
                    else failures = 0;
                }
                catch (Exception ex)
                {
                    failures++;
                    if (failures == 1) _log.Warn("读取急停输入失败：" + ex.Message);
                    if (_opt.EmergencyFailSafe && failures >= FailSafeCount && !_safety.IsEmergencyStopped)
                        Trigger("急停输入读取连续失败 " + failures + " 次，按安全侧处理：" + ex.Message);
                }

                try { await Task.Delay(_opt.EmergencyPollMs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void Trigger(string reason)
        {
            try
            {
                _log.Error("[EStop] " + reason);
                _audit.Record(AuditCategory.Operation, "EmergencyStop", "external-io", detail: reason);
                _events.GetEvent<EmergencyStopEvent>().Publish(new EmergencyStopMessage { IsActive = true, Reason = reason });
            }
            catch (Exception ex) { _log.Error("[EStop] 发布急停事件失败", ex); }       // 监控循环必须继续
        }
    }
}

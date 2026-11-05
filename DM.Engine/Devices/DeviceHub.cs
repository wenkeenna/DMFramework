using DM.Core.Devices;
using DM.Core.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Engine.Devices
{
    /// <summary>连接/断开都放进线程池：第三方库同步阻塞也只占线程池线程，不卡 UI。断开有总超时，每个设备独立 try/catch，不保证顺序。</summary>
    public sealed class DeviceHub : IDeviceHub
    {
        private readonly ConcurrentDictionary<string, IDeviceSession> _devices =
            new ConcurrentDictionary<string, IDeviceSession>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DeviceState> _states =
            new ConcurrentDictionary<string, DeviceState>(StringComparer.OrdinalIgnoreCase);
        private readonly IAppLogger _log;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public DeviceHub(IAppLogger log) { _log = log; }

        public event EventHandler<DeviceStateChangedEventArgs> StateChanged;

        public void Register(IDeviceSession device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            if (_devices.TryAdd(device.Name, device)) _states[device.Name] = DeviceState.Idle;
        }

        public DeviceState GetState(string name) { DeviceState s; return _states.TryGetValue(name ?? "", out s) ? s : DeviceState.Idle; }

        public void ConnectAllInBackground()
        {
            foreach (IDeviceSession d in _devices.Values.ToList())
            {
                IDeviceSession dev = d;
                Task.Run(() => ConnectOneAsync(dev));       // 故意不 await，异常在内部消化
            }
        }

        private async Task ConnectOneAsync(IDeviceSession dev)
        {
            SetState(dev.Name, DeviceState.Connecting, null);
            try { await dev.ConnectAsync(_cts.Token).ConfigureAwait(false); SetState(dev.Name, DeviceState.Connected, null); }
            catch (Exception ex) { _log.Error("设备连接失败：" + dev.Name, ex); SetState(dev.Name, DeviceState.Faulted, ex.Message); }
        }

        public async Task<IReadOnlyList<string>> DisconnectAllAsync(TimeSpan timeout)
        {
            _cts.Cancel();                                  // 通知仍在连接中的设备放弃
            var pending = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            var tasks = _devices.Values.Select(dev =>
            {
                pending[dev.Name] = true;
                return Task.Run(async () =>
                {
                    SetState(dev.Name, DeviceState.Disconnecting, null);
                    try { await dev.DisconnectAsync().ConfigureAwait(false); }
                    catch (Exception ex) { _log.Error("设备断开失败：" + dev.Name, ex); }
                    finally { bool ignored; pending.TryRemove(dev.Name, out ignored); SetState(dev.Name, DeviceState.Idle, null); }
                });
            }).ToList();

            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(timeout)).ConfigureAwait(false);

            List<string> left = pending.Keys.ToList();
            if (left.Count > 0) _log.Warn("以下设备断开超时，已放弃等待：" + string.Join(",", left));
            return left;
        }

        private void SetState(string name, DeviceState state, string error)
        {
            _states[name] = state;
            var h = StateChanged;
            if (h != null) h(this, new DeviceStateChangedEventArgs { Name = name, State = state, Error = error });
        }
    }
}

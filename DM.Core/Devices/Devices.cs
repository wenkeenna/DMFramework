using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Core.Devices
{
    public enum DeviceState { Idle, Connecting, Connected, Faulted, Disconnecting }

    /// <summary>
    /// 需要连接/断开的设备（运动卡、相机、仪器）。ConnectAsync 失败抛异常即可；
    /// DisconnectAsync 允许很慢（设备中心有超时保护）；两者都要幂等。
    /// </summary>
    public interface IDeviceSession
    {
        string Name { get; }
        Task ConnectAsync(CancellationToken ct);
        Task DisconnectAsync();
    }

    public sealed class DeviceStateChangedEventArgs : EventArgs
    {
        public string Name { get; set; }
        public DeviceState State { get; set; }
        public string Error { get; set; }
    }

    public interface IDeviceHub
    {
        void Register(IDeviceSession device);
        void ConnectAllInBackground();                                      // 不阻塞，进度走 StateChanged
        Task<IReadOnlyList<string>> DisconnectAllAsync(TimeSpan timeout);   // 到时就返回，给出未完成的设备名
        DeviceState GetState(string name);
        event EventHandler<DeviceStateChangedEventArgs> StateChanged;
    }
}

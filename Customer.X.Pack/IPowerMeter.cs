using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Customer.X.Pack
{
    /// <summary>功率计服务：由客户的仪器驱动实现并在 App 里注册。没有注册时，命令和任务运行会提示"无法获取服务 IPowerMeter"。</summary>
    public interface IPowerMeter
    {
        Task SetPowerAsync(string channel, double dbm, CancellationToken ct);
        Task<double> ReadPowerAsync(string channel, CancellationToken ct);
    }

    public static class PmChannels { public static readonly string[] All = { "CH1", "CH2" }; }

}

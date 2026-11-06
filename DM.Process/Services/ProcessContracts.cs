using DM.Process.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Process.Services
{
    /// <summary>力传感器：由仪器模块或扩展包实现并注册。通道名如 LeftX、LeftZ、RightZ、GlueZ，具体有哪些由实现决定。</summary>
    public interface IForceSource
    {
        bool IsConnected { get; }
        IReadOnlyList<string> Channels { get; }
        double Read(string channel);                    // 通道不存在或数据不可用时抛异常
    }

    /// <summary>
    /// 延迟获取 IForceSource：没有安装仪器模块时，点胶任务、编辑界面、模块本身依然可以正常加载，
    /// 只有真正用到力传感器时才报"没有可用的力传感器"。
    /// </summary>
    public sealed class ForceSourceProvider
    {
        private readonly Lazy<IForceSource> _src;
        public ForceSourceProvider(Lazy<IForceSource> src) { _src = src; }

        public IForceSource Get()
        {
            try { return _src.Value; }
            catch (Exception ex)
            { throw new InvalidOperationException("没有可用的力传感器（需要仪器模块或扩展包注册 IForceSource）：" + ex.Message, ex); }
        }

        public IReadOnlyList<string> ChannelsOrEmpty()
        {
            try { return _src.Value.Channels; }
            catch (Exception) { return new string[0]; }
        }
    }

    public interface IProbeService
    {
        /// <summary>逐步靠近直到力达到触发值，返回接触位置。超过最大行程仍未触发则抛异常。</summary>
        Task<double> ProbeAsync(ProbeParams p, Action<string> log, CancellationToken ct);
    }

    public interface IGlueService
    {
        Task RunAsync(GlueJob job, Action<string> log, CancellationToken ct);
    }
}

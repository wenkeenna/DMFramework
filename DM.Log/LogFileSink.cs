using DM.Core.Common;
using DM.Core.Devices;
using DM.Core.Events;
using DM.Core.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreLevel = DM.Core.Logging.LogLevel;
using NLogLevel = NLog.LogLevel;

namespace DM.Log
{
    /// <summary>
    /// 文件写入走 NLog 异步包装：调用线程只入队，不等磁盘；队列满时丢弃，不拖慢设备。
    /// 订阅用强引用（Prism 默认是弱引用，订阅者被回收后日志会静默中断）。
    /// 同时是 IDeviceSession：退出时由设备中心限时刷盘。
    /// 注意：本对象创建之前发布的日志不会被写入，所以 LogModule 要排在最前面加载。
    /// </summary>
    public sealed class LogFileSink : IDeviceSession
    {
        public LogFileSink(IEventAggregator events, AppPaths paths)
        {
            Configure(paths.Logs);
            events.GetEvent<AppLogEvent>().Subscribe(Write, ThreadOption.PublisherThread, true);
        }

        private static void Configure(string dir)
        {
            Directory.CreateDirectory(dir);
            var file = new FileTarget("file")
            {
                FileName = Path.Combine(dir, "app-${shortdate}.log"),
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}",
                Encoding = System.Text.Encoding.UTF8,
                ArchiveAboveSize = 10 * 1024 * 1024,
                MaxArchiveFiles = 60,
                KeepFileOpen = true
            };
            var async = new AsyncTargetWrapper("async", file)
            { QueueLimit = 10000, OverflowAction = AsyncTargetWrapperOverflowAction.Discard };

            var cfg = new LoggingConfiguration();
            cfg.AddTarget(async);
            cfg.AddRule(NLogLevel.Debug, NLogLevel.Fatal, async);
            LogManager.Configuration = cfg;
        }

        private static void Write(LogEventMessage m)
        {
            if (m == null) return;
            NLogLevel level = m.Level == CoreLevel.Error ? NLogLevel.Error
                            : m.Level == CoreLevel.Warn ? NLogLevel.Warn
                            : m.Level == CoreLevel.Debug ? NLogLevel.Debug : NLogLevel.Info;
            string text = m.Detail == null ? m.Message : m.Message + Environment.NewLine + m.Detail;
            LogManager.GetLogger(string.IsNullOrWhiteSpace(m.Title) ? "App" : m.Title).Log(level, text);
        }

        public string Name => "Log";
        public Task ConnectAsync(CancellationToken ct) { return Task.CompletedTask; }
        public Task DisconnectAsync() { return Task.Run(() => LogManager.Flush(TimeSpan.FromSeconds(2))); }    // 只刷盘不关闭
    }
}

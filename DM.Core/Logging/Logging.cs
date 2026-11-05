using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Core.Logging
{
    public enum LogLevel { Debug, Info, Warn, Error }

    public sealed class LogEventMessage
    {
        public LogLevel Level { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }          // 异常完整文本，可为空
        public DateTime TimestampUtc { get; set; }
    }

    /// <summary>技术日志（排障用）。操作留痕用 IAuditService，两者分开存储。</summary>
    public interface IAppLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception ex = null);
    }
}

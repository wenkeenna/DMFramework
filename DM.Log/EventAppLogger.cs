using DM.Core.Events;
using DM.Core.Logging;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Log
{
    public sealed class EventAppLogger : IAppLogger
    {
        private readonly IEventAggregator _events;
        public EventAppLogger(IEventAggregator events) { _events = events; }

        public void Info(string message) { Publish(LogLevel.Info, message, null); }
        public void Warn(string message) { Publish(LogLevel.Warn, message, null); }
        public void Error(string message, Exception ex = null) { Publish(LogLevel.Error, message, ex == null ? null : ex.ToString()); }

        private void Publish(LogLevel level, string message, string detail)
        {
            try
            {
                _events.GetEvent<AppLogEvent>().Publish(new LogEventMessage
                { Level = level, Title = "System", Message = message, Detail = detail, TimestampUtc = DateTime.UtcNow });
            }
            catch (Exception) { /* 日志本身永不抛异常 */ }
        }
    }
}

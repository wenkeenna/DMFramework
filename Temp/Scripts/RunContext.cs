using DM.Core.Common;
using DM.Core.Events;
using DM.Core.Logging;
using Prism.Events;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.Scripts
{
    public sealed class RunContext : IRunContext
    {
        private readonly IEventAggregator _events;

        public RunContext(IContainerProvider container, IEventAggregator events, Guid runId, string taskName)
        { Container = container; _events = events; RunId = runId; TaskName = taskName; }

        public IContainerProvider Container { get; }
        public Guid RunId { get; }
        public string TaskName { get; }

        public void Log(string message)
        {
            _events.GetEvent<AppLogEvent>().Publish(new LogEventMessage
            { Level = LogLevel.Info, Message = message, TimestampUtc = DateTime.UtcNow, Title = TaskName });
        }
    }
}

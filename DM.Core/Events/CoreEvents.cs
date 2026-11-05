using DM.Core.Logging;
using DM.Core.Projects;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Core.Events
{
    /// <summary>任何地方发布，日志模块订阅写文件，日志界面订阅显示。</summary>
    public sealed class AppLogEvent : PubSubEvent<LogEventMessage> { }

    public sealed class EmergencyStopMessage
    {
        public bool IsActive { get; set; }          // true=急停触发，false=解除
        public string Reason { get; set; }
    }
    public sealed class EmergencyStopEvent : PubSubEvent<EmergencyStopMessage> { }

    /// <summary>只给不需要排阶段的轻量订阅者。有顺序要求的初始化一律走 IProjectAware。</summary>
    public sealed class ProjectOpenedEvent : PubSubEvent<ProjectContext> { }
    public sealed class ProjectClosedEvent : PubSubEvent<string> { }
}

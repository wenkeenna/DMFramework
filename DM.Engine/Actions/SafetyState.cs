using DM.Core.Actions;
using DM.Core.Events;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.Actions
{
    public sealed class SafetyState : ISafetyState
    {
        private volatile bool _stopped;
        private volatile string _reason;

        public SafetyState(IEventAggregator events)
        {
            events.GetEvent<EmergencyStopEvent>().Subscribe(m =>
            { if (m == null) return; _reason = m.Reason; _stopped = m.IsActive; }, ThreadOption.PublisherThread);
        }

        public bool IsEmergencyStopped => _stopped;
        public string Reason => _reason;
    }
}

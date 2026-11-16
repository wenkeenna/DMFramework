using DM.Motion.Leadshine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Customer.X.Pack.Motion
{
    public static class XLeadshine
    {
        public static LeadshineOptions Create() => new LeadshineOptions
        {
            CardConfigFiles = { { 0, "ControlCardConfig_0.ini" }, { 1, "ControlCardConfig_1.ini" } },
            EmergencyInput = Io.EStop,              // 用常量，名字改了编译会报错
            EmergencyPollMs = 50,
            EmergencyFailSafe = true,
            AutoEnable = true,
            StartVelocityRatio = 0.3,
            LineCoordinate = 0
        };
    }
}

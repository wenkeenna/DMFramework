using DM.Core.Common;
using DM.Core.Logging;
using DM.Engine.Hosting;
using DM.Motion.Leadshine.Drivers;
using DM.Motion.Module;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Leadshine
{
    [ModuleDependency("MotionModule")]
    public sealed class LeadshineMotionModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<ILtdmcApi, LtdmcApi>();
            r.RegisterSingleton<LeadshineOptions>(c => c.Resolve<IMachineConfig>().Get<LeadshineOptions>("Leadshine"));
            r.RegisterSingleton<ExternalEmergencyStopMonitor>();
        }

        protected override void Attach(ModuleScope s)
        {
            var api = s.Resolve<ILtdmcApi>();
            var opt = s.Resolve<LeadshineOptions>();
            var paths = s.Resolve<AppPaths>();
            var log = s.Resolve<IAppLogger>();

            s.MotionDriver("Leadshine", () => new LeadshineDriver(api, opt, paths, log));      // machine.json 里 Motion.Driver 写 "Leadshine"

            if (!string.IsNullOrWhiteSpace(opt.EmergencyInput)) s.Device<ExternalEmergencyStopMonitor>();
            else log.Warn("未配置 Leadshine.EmergencyInput，外部急停监控未启用。");
        }
    }
}

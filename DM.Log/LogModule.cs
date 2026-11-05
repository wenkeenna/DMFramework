using DM.Core.Logging;
using DM.Engine.Hosting;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Log
{
    public sealed class LogModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<IAppLogger, EventAppLogger>();
            r.RegisterSingleton<LogFileSink>();
        }

        protected override void Attach(ModuleScope s) { s.Device<LogFileSink>(); }      // 解析即创建，订阅生效
    }
}

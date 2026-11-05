using DM.Engine.Hosting;
using DM.Motion.Commands;
using DM.Motion.Services;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Module
{
    public sealed class MotionModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<IMotionDriverCatalog, MotionDriverCatalog>();
            r.RegisterSingleton<MotionService>();
            r.RegisterSingleton<IMotionService>(c => c.Resolve<MotionService>());   // 与设备中心拿到同一实例
            r.RegisterSingleton<ManualMotionService>();

            r.RegisterSingleton<MoveAbsCommand>(); r.RegisterSingleton<MoveRelCommand>();
            r.RegisterSingleton<HomeCommand>(); r.RegisterSingleton<MoveMultiCommand>();
            r.RegisterSingleton<SetOutCommand>(); r.RegisterSingleton<WaitInCommand>();
        }

        protected override void Attach(ModuleScope s)
        {
            s.Device<MotionService>()
             .Command<MoveAbsCommand>().Command<MoveRelCommand>().Command<HomeCommand>()
             .Command<MoveMultiCommand>().Command<SetOutCommand>().Command<WaitInCommand>();
        }
    }
}

using DM.Engine.Hosting;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Customer.X.Pack
{
    public sealed class CustomerXPackModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<SetPmCommand>(); r.RegisterSingleton<ReadPmCommand>();
            r.RegisterSingleton<ReadPowerTaskType>();
            r.RegisterForNavigation<ReadPowerEditorView, ReadPowerEditorViewModel>("ReadPowerEditorView");
        }

        protected override void Attach(ModuleScope s)
        { s.TaskType<ReadPowerTaskType>().Command<SetPmCommand>().Command<ReadPmCommand>(); }
    }

}

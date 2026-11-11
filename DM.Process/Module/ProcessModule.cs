using DM.Engine.Hosting;
using DM.Process.Editors;
using DM.Process.Services;
using DM.Process.Tasks;
using DM.Scripting.Commands;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Process.Module
{
    [ModuleDependency("MotionModule")]
    [ModuleDependency("ScriptingModule")]
    public sealed class ProcessModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<ForceSourceProvider>();
            r.RegisterSingleton<IProbeService, ProbeService>();
            r.RegisterSingleton<IGlueService, GlueService>();

            r.RegisterSingleton<GlueTaskType>(); r.RegisterSingleton<TouchProbeTaskType>();
            r.RegisterSingleton<ProbeCommand>(); r.RegisterSingleton<GluePointCommand>(); r.RegisterSingleton<GlueLineCommand>();
            r.RegisterSingleton<GlueDefaultsSeeder>();

            r.RegisterForNavigation<GlueEditorView, GlueEditorViewModel>("GlueEditorView");
            r.RegisterForNavigation<TouchProbeEditorView, TouchProbeEditorViewModel>("TouchProbeEditorView");
        }

        protected override void Attach(ModuleScope s)
        {
            s.TaskType<GlueTaskType>().TaskType<TouchProbeTaskType>()
             .Command<ProbeCommand>().Command<GluePointCommand>().Command<GlueLineCommand>()
             .Seeder<GlueDefaultsSeeder>();
        }
    }
}

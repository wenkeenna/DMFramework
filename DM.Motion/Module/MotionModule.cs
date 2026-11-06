using DM.Engine.Hosting;
using DM.Motion.Commands;
using DM.Motion.Services;
using DM.Motion.TaskEditors;
using DM.Motion.Tasks;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Module
{
    [ModuleDependency("ScriptingModule")]
    public sealed class MotionModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<IMotionDriverCatalog, MotionDriverCatalog>();
            r.RegisterSingleton<MotionService>();
            r.RegisterSingleton<IMotionService>(c => c.Resolve<MotionService>());       // 与设备中心拿到同一实例
            r.RegisterSingleton<ManualMotionService>();

            r.RegisterSingleton<MoveAbsCommand>(); r.RegisterSingleton<MoveRelCommand>(); r.RegisterSingleton<HomeCommand>();
            r.RegisterSingleton<MoveMultiCommand>(); r.RegisterSingleton<Line2Command>();
            r.RegisterSingleton<SetOutCommand>(); r.RegisterSingleton<WaitInCommand>();

            r.RegisterSingleton<SingleAxisTaskType>(); r.RegisterSingleton<MultiAxisTaskType>();
            r.RegisterSingleton<Line2TaskType>(); r.RegisterSingleton<IoExecutionTaskType>();

            r.RegisterForNavigation<SingleAxisEditorView, SingleAxisEditorViewModel>("SingleAxisEditorView");
            r.RegisterForNavigation<MultiAxisEditorView, MultiAxisEditorViewModel>("MultiAxisEditorView");
            r.RegisterForNavigation<Line2EditorView, Line2EditorViewModel>("Line2EditorView");
            r.RegisterForNavigation<IoExecutionEditorView, IoExecutionEditorViewModel>("IoExecutionEditorView");
        }

        protected override void Attach(ModuleScope s)
        {
            s.Device<MotionService>()
             .TaskType<SingleAxisTaskType>().TaskType<MultiAxisTaskType>().TaskType<Line2TaskType>().TaskType<IoExecutionTaskType>()
             .Command<MoveAbsCommand>().Command<MoveRelCommand>().Command<HomeCommand>()
             .Command<MoveMultiCommand>().Command<Line2Command>().Command<SetOutCommand>().Command<WaitInCommand>();
        }
    }

    public static class MotionScopeExtensions
    {
        /// <summary>驱动模块用它登记自己：scope.MotionDriver("Leadshine", () => new LeadshineDriver())。</summary>
        public static ModuleScope MotionDriver(this ModuleScope s, string name, Func<IMotionDriver> factory)
        { s.Resolve<IMotionDriverCatalog>().Add(name, factory); return s; }
    }
}

using DM.Core.Scripting;
using DM.Engine.Hosting;
using DM.Scripting.Services;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Scripting.Commands
{
    public sealed class ScriptingModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<IScriptCommandRegistry, ScriptCommandRegistry>();
            r.RegisterSingleton<ScriptCompletion>();
            r.RegisterSingleton<ScriptTaskType>();
            r.RegisterSingleton<LogCommand>();
            r.RegisterSingleton<DelayCommand>();
        }

        protected override void Attach(ModuleScope s)
        { s.TaskType<ScriptTaskType>().Command<LogCommand>().Command<DelayCommand>(); }
    }

    public static class ScriptingScopeExtensions
    {
        /// <summary>功能模块和扩展包贡献脚本命令：scope.Command&lt;MoveAbsCommand&gt;()。</summary>
        public static ModuleScope Command<T>(this ModuleScope s) where T : IScriptCommand
        {
            s.Resolve<IScriptCommandRegistry>().Register(s.Resolve<T>());
            return s;
        }
    }
}

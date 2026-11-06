using DM.Engine.Hosting;
using DM.Scripting.UI.ViewModels;
using DM.Scripting.UI.Views;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Scripting.UI.Module
{
    public sealed class ScriptingUiModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        { r.RegisterForNavigation<ScriptEditorView, ScriptEditorViewModel>("ScriptEditorView"); }     // 名字 = ScriptTaskType.EditorView
    }
}

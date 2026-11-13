using DM.Engine.Hosting;
using DM.Parameters.ViewModels;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Parameters.Module
{
    public sealed class ParametersModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        { 
           // r.RegisterDialog<ParameterEditorDialog, ParameterEditorViewModel>("ParameterEditorDialog"); 
        }
    }
}

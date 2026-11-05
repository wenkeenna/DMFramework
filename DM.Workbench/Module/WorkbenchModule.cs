using DM.Engine.Hosting;
using DM.Workbench.Services;
using DM.Workbench.ViewModels;
using DM.Workbench.Views;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Workbench.Module
{
    public sealed class WorkbenchModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<TaskListEditor>();
            r.RegisterSingleton<TaskListProjectPart>();
            r.RegisterSingleton<TaskListViewModel>();               // 单例：多次创建视图也只订阅一次运行器事件

            r.RegisterForNavigation<TaskListView, TaskListViewModel>();
            r.RegisterForNavigation<EmptyTaskView>("EmptyTaskView");

            r.RegisterDialog<TaskTypePickerDialog, TaskTypePickerViewModel>("TaskTypePickerDialog");
            r.RegisterDialog<NewProjectDialog, NewProjectDialogViewModel>("NewProjectDialog");
            r.RegisterDialog<OpenProjectDialog, OpenProjectDialogViewModel>("OpenProjectDialog");
        }

        protected override void Attach(ModuleScope s) { s.ProjectPart<TaskListProjectPart>(); }
    }
}

using DM.Core.Projects;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Workbench.ViewModels
{
    public sealed class NewProjectDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IProjectCatalog _catalog;
        private string _name = "", _message = "";

        public NewProjectDialogViewModel(IProjectCatalog catalog)
        {
            _catalog = catalog;
            ConfirmCommand = new DelegateCommand(OnConfirm);
            CancelCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

        public string Title => "新建项目";
        public event Action<IDialogResult> RequestClose;
        public string ProjectName { get { return _name; } set { SetProperty(ref _name, value ?? ""); } }
        public string Message { get { return _message; } private set { SetProperty(ref _message, value); } }
        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private void OnConfirm()
        {
            string err;
            ProjectInfo p = _catalog.Create(ProjectName.Trim(), out err);
            if (p == null) { Message = err; return; }
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, new DialogParameters { { "ProjectName", p.Name } }));
        }

        public bool CanCloseDialog() { return true; }
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters p) { ProjectName = ""; Message = ""; }
    }
}

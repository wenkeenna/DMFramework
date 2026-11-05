using DM.Core.Projects;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Workbench.ViewModels
{
    public sealed class OpenProjectDialogViewModel : BindableBase, IDialogAware
    {
        private readonly IProjectCatalog _catalog;
        private ProjectInfo _selected;
        private string _message = "";

        public OpenProjectDialogViewModel(IProjectCatalog catalog)
        {
            _catalog = catalog;
            ConfirmCommand = new DelegateCommand(OnConfirm, () => Selected != null).ObservesProperty(() => Selected);
            CancelCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

        public string Title => "打开项目";
        public event Action<IDialogResult> RequestClose;
        public ObservableCollection<ProjectInfo> Projects { get; } = new ObservableCollection<ProjectInfo>();
        public ProjectInfo Selected { get { return _selected; } set { SetProperty(ref _selected, value); } }
        public string Message { get { return _message; } private set { SetProperty(ref _message, value); } }
        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private void OnConfirm() { RequestClose?.Invoke(new DialogResult(ButtonResult.OK, new DialogParameters { { "ProjectName", Selected.Name } })); }

        public bool CanCloseDialog() { return true; }
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters p)
        {
            Projects.Clear();
            foreach (ProjectInfo i in _catalog.List()) Projects.Add(i);
            Selected = Projects.FirstOrDefault();
            Message = Projects.Count == 0 ? "没有可打开的项目，请先新建。" : "";
        }
    }
}

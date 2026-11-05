using DM.Core.TaskList;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DM.Workbench.ViewModels
{
    /// <summary>列出所有已注册的任务类型（含扩展包里的），按 Group 分组。</summary>
    public sealed class TaskTypePickerViewModel : BindableBase, IDialogAware
    {
        private ITaskType _selected;

        public TaskTypePickerViewModel(ITaskTypeRegistry types)
        {
            var view = new ListCollectionView(types.All.ToList());
            view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            Types = view;
            PickCommand = new DelegateCommand(Pick, () => Selected != null).ObservesProperty(() => Selected);
            CancelCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

        public string Title => "添加任务";
        public event Action<IDialogResult> RequestClose;
        public ICollectionView Types { get; }
        public ITaskType Selected { get { return _selected; } set { SetProperty(ref _selected, value); } }
        public DelegateCommand PickCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private void Pick() { RequestClose?.Invoke(new DialogResult(ButtonResult.OK, new DialogParameters { { "TypeKey", Selected.Key } })); }

        public bool CanCloseDialog() { return true; }
        public void OnDialogClosed() { }
        public void OnDialogOpened(IDialogParameters p) { }
    }
}

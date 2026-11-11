using DM.Core.TaskList;
using DM.Motion.Services;
using DM.Motion.Tasks;
using DM.UI.TaskEditing;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace DM.Motion.TaskEditors
{
    /// <summary>运动类编辑器的公共基类：提供轴名、IO 名下拉列表（来自 machine.json）。</summary>
    public abstract class MotionEditorViewModel<TData> : TaskEditorViewModelBase<TData> where TData : BindableBase, new()
    {
        protected MotionEditorViewModel(ITaskTypeRegistry types, string typeKey, IMotionService motion) : base(types, typeKey)
        {
            AxisNames = motion.Axes.Select(a => a.Name).ToList();
            IoNames = motion.Inputs.Select(i => i.Name).Concat(motion.Outputs.Select(o => o.Name)).ToList();
        }

        public IReadOnlyList<string> AxisNames { get; }
        public IReadOnlyList<string> IoNames { get; }
    }

    public sealed class SingleAxisEditorViewModel : MotionEditorViewModel<SingleAxisData>
    {
        public SingleAxisEditorViewModel(ITaskTypeRegistry t, IMotionService m) : base(t, SingleAxisTaskType.TypeKey, m) { }
        public Array Modes { get; } = Enum.GetValues(typeof(AxisMoveMode));
    }

    public sealed class Line2EditorViewModel : MotionEditorViewModel<Line2Data>
    {
        public Line2EditorViewModel(ITaskTypeRegistry t, IMotionService m) : base(t, Line2TaskType.TypeKey, m) { }
    }

    public sealed class IoExecutionEditorViewModel : MotionEditorViewModel<IoExecutionData>
    {
        public IoExecutionEditorViewModel(ITaskTypeRegistry t, IMotionService m) : base(t, IoExecutionTaskType.TypeKey, m) { }
    }

    public sealed class MultiAxisEditorViewModel : MotionEditorViewModel<MultiAxisData>
    {
        private MultiAxisItem _selected;

        public MultiAxisEditorViewModel(ITaskTypeRegistry t, IMotionService m) : base(t, MultiAxisTaskType.TypeKey, m)
        {
            AddCommand = new DelegateCommand(() =>
            {
                var item = new MultiAxisItem { Priority = Data.Items.Count == 0 ? 1 : Data.Items.Max(x => x.Priority) + 1 };
                Data.Items.Add(item);
                SelectedItem = item;
            }, () => IsEditable).ObservesProperty(() => IsEditable);

            RemoveCommand = new DelegateCommand(() => { if (SelectedItem != null) Data.Items.Remove(SelectedItem); },
                () => IsEditable && SelectedItem != null).ObservesProperty(() => IsEditable).ObservesProperty(() => SelectedItem);
        }

        public MultiAxisItem SelectedItem { get { return _selected; } set { SetProperty(ref _selected, value); } }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand RemoveCommand { get; }

        protected override void OnLoaded() { SelectedItem = Data.Items.FirstOrDefault(); }
    }
}

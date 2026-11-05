using DM.Workbench.Models;
using DM.Workbench.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DM.Workbench.Views
{
    /// <summary>
    /// TaskListView.xaml 的交互逻辑
    /// </summary>
    public partial class TaskListView : UserControl
    {
        private const string DragFormat = "DM.Workbench.TaskRow";
        private Point _start;
        private TaskRow _dragRow;
        public TaskListView()
        {
            InitializeComponent();
        }

        private TaskListViewModel Vm { get { return DataContext as TaskListViewModel; } }

        /// <summary>选中项变化（含程序自动选中）时滚动到可见位置，运行时能看到当前任务。</summary>
        private void TaskListBox_SelectionChanged(object s, SelectionChangedEventArgs e)
        { if (TaskListBox.SelectedItem != null) TaskListBox.ScrollIntoView(TaskListBox.SelectedItem); }

        // ───── 拖拽排序：运行中或无权限时不启动拖拽 ─────
        private void TaskListBox_PreviewMouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            _start = e.GetPosition(null);
            _dragRow = RowAt(e.OriginalSource as DependencyObject);
        }

        private void TaskListBox_PreviewMouseMove(object s, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragRow == null) return;
            TaskListViewModel vm = Vm;
            if (vm == null || !vm.CanEdit || vm.IsRunning) { _dragRow = null; return; }

            Vector d = _start - e.GetPosition(null);
            if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            TaskRow row = _dragRow; _dragRow = null;
            DragDrop.DoDragDrop(TaskListBox, new DataObject(DragFormat, row), DragDropEffects.Move);
        }

        private void TaskListBox_DragOver(object s, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void TaskListBox_Drop(object s, DragEventArgs e)
        {
            var row = e.Data.GetData(DragFormat) as TaskRow;
            TaskListViewModel vm = Vm;
            if (row == null || vm == null) return;

            TaskRow target = RowAt(e.OriginalSource as DependencyObject);
            int index = target != null ? vm.TaskList.IndexOf(target) : vm.TaskList.Count - 1;     // 拖到空白处放到末尾
            vm.MoveTo(row, index);
            e.Handled = true;
        }

        private static TaskRow RowAt(DependencyObject d)
        {
            while (d != null && !(d is ListBoxItem))
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            var item = d as ListBoxItem;
            return item == null ? null : item.DataContext as TaskRow;
        }



    }
}

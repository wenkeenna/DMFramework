using DM.Parameters.Models;
using DM.Parameters.ViewModels;
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

namespace DM.Parameters.Views
{
    /// <summary>
    /// ParameterEditorDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ParameterEditorDialog : UserControl
    {
        private bool isDragging = false;
        private Point startPoint;
        public ParameterEditorDialog()
        {
            InitializeComponent();

            #region 窗口拖拽
            this.MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    isDragging = true;
                    startPoint = e.GetPosition(this);
                    Mouse.Capture(this);
                }
            };

            this.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    var currentPosition = e.GetPosition(this);
                    var delta = currentPosition - startPoint;

                    var window = Window.GetWindow(this);

                    if (window != null)
                    {
                        window.Left += delta.X;
                        window.Top += delta.Y;
                    }
                }


            };

            this.MouseUp += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    Mouse.Capture(null);
                }
            };

            #endregion

        }

        private ParameterEditorViewModel Vm { get { return DataContext as ParameterEditorViewModel; } }
        private static ParamLeafNode Leaf(object sender) { return (sender as FrameworkElement)?.DataContext as ParamLeafNode; }

        private void Tree_SelectedItemChanged(object s, RoutedPropertyChangedEventArgs<object> e)
        { if (Vm != null) Vm.Selected = e.NewValue as ParamNode; }

        /// <summary>F2：编辑选中的参数；Delete：删除；Enter 在组上：展开或折叠。</summary>
        private void Tree_PreviewKeyDown(object s, KeyEventArgs e)
        {
            if (Vm == null || e.OriginalSource is TextBox) return;               // 正在文本框里输入时不拦截
            var leaf = Vm.Selected as ParamLeafNode;
            if (e.Key == Key.F2 && leaf != null) { Vm.BeginEdit(leaf); e.Handled = true; }
            else if (e.Key == Key.Delete && Vm.DeleteCommand.CanExecute()) { Vm.DeleteCommand.Execute(); e.Handled = true; }
        }

        /// <summary>双击值进入编辑（单击只是选中）。</summary>
        private void ValueText_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 || Vm == null) return;
            Vm.BeginEdit(Leaf(s));
            e.Handled = true;
        }

        private void Editor_IsVisibleChanged(object s, DependencyPropertyChangedEventArgs e)
        {
            var box = s as TextBox;
            if (box != null && box.IsVisible) box.Dispatcher.BeginInvoke(new System.Action(() => { box.Focus(); box.SelectAll(); }));
        }

        private void Editor_KeyDown(object s, KeyEventArgs e)
        {
            var box = s as TextBox; var leaf = Leaf(s);
            if (Vm == null || leaf == null || box == null) return;

            if (e.Key == Key.Enter) { Commit(leaf, box); e.Handled = true; }
            else if (e.Key == Key.Escape) { Vm.CancelEdit(leaf); Tree.Focus(); e.Handled = true; }
        }

        /// <summary>失焦也提交（与旧版一致），但失焦不重复提交：回车已经把 IsEditing 置为 false 的情况下不再处理。</summary>
        private void Editor_LostKeyboardFocus(object s, KeyboardFocusChangedEventArgs e)
        {
            var box = s as TextBox; var leaf = Leaf(s);
            if (Vm == null || leaf == null || box == null || !leaf.IsEditing) return;
            if (!Commit(leaf, box)) Vm.CancelEdit(leaf);                          // 失焦时输入无效：放弃修改，不把用户卡在编辑状态
        }

        private bool Commit(ParamLeafNode leaf, TextBox box)
        {
            string error;
            if (Vm.TryApply(leaf, box.Text, out error)) { Tree.Focus(); return true; }
            Vm.Report(error);
            return false;                                                         // 回车时无效：保持编辑，用户可以改
        }

        private void BoolCheck_Click(object s, RoutedEventArgs e)
        {
            var leaf = Leaf(s); var box = s as CheckBox;
            if (Vm != null && leaf != null && box != null) Vm.ToggleBool(leaf, box.IsChecked == true);
        }



    }
}

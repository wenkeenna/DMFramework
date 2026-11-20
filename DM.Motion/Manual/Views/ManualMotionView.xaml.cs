using DM.Motion.Manual.ViewModels;
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

namespace DM.Motion.Manual.Views
{
    /// <summary>
    /// ManualMotionView.xaml 的交互逻辑
    /// </summary>
    public partial class ManualMotionView : UserControl
    {
        private readonly ManualMotionViewModel _vm;

        public ManualMotionView(ManualMotionViewModel vm)
        {
            InitializeComponent();

            DataContext = _vm = vm;
            Loaded += (s, e) => _vm.Start();           // 显示时才轮询
            Unloaded += (s, e) => _vm.Stop();          // 卸载时停止轮询，并停掉连续点动
        }

        private static SlotVm Slot(object s) { return (s as FrameworkElement)?.DataContext as SlotVm; }

        private void Jog_Down(object sender, MouseButtonEventArgs e)
        {

        }

        private void Jog_Up(object sender, MouseButtonEventArgs e)
        {

        }
        //public void Jog_Down(object s, MouseButtonEventArgs e) { _vm.Down(Slot(s), (Keyboard.Modifiers & ModifierKeys.Control) != 0); }
        //public void Jog_Up(object s, MouseEventArgs e) { _vm.Up(Slot(s)); }



    }
}

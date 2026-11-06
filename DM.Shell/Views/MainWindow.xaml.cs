using DM.Shell.ViewModels;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace DM.Shell.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(IRegionManager regions)
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                // 名字由 DM.Workbench 通过 RegisterForNavigation 注册；Shell 只认名字，不依赖具体视图类型
                regions.RequestNavigate("TaskListRegion", "TaskListView");
                regions.RequestNavigate("TaskDetailRegion", "EmptyTaskView");
            };
        }

        /// <summary>关闭 = 确认后立即隐藏窗口，在后台保存、断开设备再结束进程，用户感觉秒关。</summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            var vm = DataContext as MainWindowViewModel;
            if (vm != null && !vm.ConfirmExit()) return;
            Hide();
            if (vm != null) vm.Exit(); else Environment.Exit(0);
        }
    }
}

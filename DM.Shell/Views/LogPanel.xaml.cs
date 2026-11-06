using DM.Shell.ViewModels;
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
using System.Windows.Threading;

namespace DM.Shell.Views
{
    /// <summary>
    /// LogPanel.xaml 的交互逻辑
    /// </summary>
    public partial class LogPanel : UserControl
    {
        public LogPanel()
        {
            InitializeComponent();
            DataContextChanged += (s, e) =>
            {
                var vm = e.NewValue as LogPanelViewModel;
                if (vm != null) vm.Rows.CollectionChanged += (a, b) => ScrollToEnd(vm);
            };
        }

        private void ScrollToEnd(LogPanelViewModel vm)
        {
            if (!vm.AutoScroll) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(() =>
            { if (LogList.Items.Count > 0) LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]); }));
        }
    }
}

using DM.Shell.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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


            ((INotifyCollectionChanged)LogList.Items).CollectionChanged += LogList_CollectionChanged;

        }

        private void LogList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // 有新日志加入时，执行滚动到底部
                ScrollToEnd(DataContext as LogPanelViewModel);
            }
        }

        private void ScrollToEnd(LogPanelViewModel vm)
        {
            if (!vm.AutoScroll) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(() =>
            {
                //if (LogList.Items.Count > 0)
                //    LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
                if (LogList.Items.Count > 0)
                {
                    if (FindVisualChild<ScrollViewer>(LogList) is ScrollViewer sv)
                        sv.ScrollToEnd();
                }

            }));
        }

        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

     
    }
}

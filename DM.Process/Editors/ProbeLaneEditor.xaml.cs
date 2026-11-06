using DM.Process.Models;
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

namespace DM.Process.Editors
{
    /// <summary>
    /// ProbeLaneEditor.xaml 的交互逻辑
    /// </summary>
    public partial class ProbeLaneEditor : UserControl
    {
        public static readonly DependencyProperty LaneProperty = DependencyProperty.Register("Lane", typeof(ProbeLaneData), typeof(ProbeLaneEditor));
        public static readonly DependencyProperty AxisNamesProperty = DependencyProperty.Register("AxisNames", typeof(IEnumerable<string>), typeof(ProbeLaneEditor));
        public static readonly DependencyProperty ChannelsProperty = DependencyProperty.Register("Channels", typeof(IEnumerable<string>), typeof(ProbeLaneEditor));

        public ProbeLaneData Lane { get { return (ProbeLaneData)GetValue(LaneProperty); } set { SetValue(LaneProperty, value); } }
        public IEnumerable<string> AxisNames { get { return (IEnumerable<string>)GetValue(AxisNamesProperty); } set { SetValue(AxisNamesProperty, value); } }
        public IEnumerable<string> Channels { get { return (IEnumerable<string>)GetValue(ChannelsProperty); } set { SetValue(ChannelsProperty, value); } }

        public ProbeLaneEditor() { InitializeComponent(); DirBox.ItemsSource = Enum.GetValues(typeof(ProbeDirection)); }
    }
}

using DM.Motion.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace DM.Motion.TaskEditors
{
    /// <summary>
    /// IoStepListEditor.xaml 的交互逻辑
    /// </summary>
    public partial class IoStepListEditor : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(IoStepListEditor), new PropertyMetadata(""));
        public static readonly DependencyProperty StepsProperty =
            DependencyProperty.Register("Steps", typeof(ObservableCollection<IoStep>), typeof(IoStepListEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty NamesProperty =
            DependencyProperty.Register("Names", typeof(IEnumerable<string>), typeof(IoStepListEditor), new PropertyMetadata(null));

        public string Title { get { return (string)GetValue(TitleProperty); } set { SetValue(TitleProperty, value); } }
        public ObservableCollection<IoStep> Steps { get { return (ObservableCollection<IoStep>)GetValue(StepsProperty); } set { SetValue(StepsProperty, value); } }
        public IEnumerable<string> Names { get { return (IEnumerable<string>)GetValue(NamesProperty); } set { SetValue(NamesProperty, value); } }

        public IoStepListEditor()
        {
            InitializeComponent();
            KindColumn.ItemsSource = Enum.GetValues(typeof(IoStepKind));
        }

        private void Add_Click(object s, RoutedEventArgs e) { if (Steps != null) Steps.Add(new IoStep()); }

        private void Remove_Click(object s, RoutedEventArgs e)
        {
            var step = StepGrid.SelectedItem as IoStep;
            if (step != null && Steps != null) Steps.Remove(step);
        }
    }
}

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
    /// TaskEditDialog.xaml 的交互逻辑
    /// </summary>
    public partial class TaskEditDialog : UserControl
    {
        private bool isDragging = false;
        private Point startPoint;
        public TaskEditDialog()
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
    }
}

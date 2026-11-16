using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace DM.UI
{
    /// <summary>简单的单行输入框。返回 null 表示取消。</summary>
    public static class Prompt
    {
        public static string Ask(string message, string initial = "", string title = "输入")
        {
            var box = new TextBox { Text = initial ?? "", Margin = new Thickness(0, 8, 0, 0), MinWidth = 260 };
            var ok = new Button { Content = "确定", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "取消", Width = 80, IsCancel = true };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            buttons.Children.Add(ok); buttons.Children.Add(cancel);
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
            panel.Children.Add(box); panel.Children.Add(buttons);

            var win = new Window
            {
                Title = title,
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current == null ? null : Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            };
            ok.Click += (s, e) => win.DialogResult = true;
            win.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
            return win.ShowDialog() == true ? box.Text : null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace DM.Identity.Behaviors
{
    /// <summary>让 PasswordBox 能绑定到 ViewModel 的字符串属性。</summary>
    public static class PasswordBoxAssist
    {
        public static readonly DependencyProperty AttachProperty = DependencyProperty.RegisterAttached(
            "Attach", typeof(bool), typeof(PasswordBoxAssist), new PropertyMetadata(false, OnAttach));

        public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
            "BoundPassword", typeof(string), typeof(PasswordBoxAssist),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundChanged));

        public static bool GetAttach(DependencyObject d) { return (bool)d.GetValue(AttachProperty); }
        public static void SetAttach(DependencyObject d, bool v) { d.SetValue(AttachProperty, v); }
        public static string GetBoundPassword(DependencyObject d) { return (string)d.GetValue(BoundPasswordProperty); }
        public static void SetBoundPassword(DependencyObject d, string v) { d.SetValue(BoundPasswordProperty, v); }

        private static void OnAttach(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pb = d as PasswordBox; if (pb == null) return;
            if ((bool)e.NewValue) pb.PasswordChanged += Changed; else pb.PasswordChanged -= Changed;
        }

        private static void OnBoundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pb = d as PasswordBox; if (pb == null) return;
            pb.PasswordChanged -= Changed;
            string v = (string)e.NewValue ?? "";
            if (pb.Password != v) pb.Password = v;          // ViewModel 清空密码时界面同步清空
            pb.PasswordChanged += Changed;
        }

        private static void Changed(object s, RoutedEventArgs e) { var pb = (PasswordBox)s; SetBoundPassword(pb, pb.Password); }
    }
}

using DM.Core.Common;
using DM.Motion.Module;
using DM.Process.Module;
using DM.Shell;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Customer.X.Pack
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : DmApp
    {
        protected override string AppTitle => "X 客户设备控制系统";

        protected override void ConfigureCustomerModules(IModuleCatalog catalog)
        {
            catalog.AddModule<MotionModule>();
            // c.AddModule<LeadshineMotionModule>();     // 雷赛驱动，待实现
            catalog.AddModule<ProcessModule>();
            catalog.AddModule<CustomerXPackModule>();
        }

        /// <summary>所有落盘位置的根目录；其下 Config、Logs、Projects 自动创建。</summary>
        protected override AppPaths CreatePaths()
        {
            return new AppPaths(@"D:\DMData\CustomerX");
        }



        /// <summary>客户专有服务在这里注册（仪器、传感器等），接口由对应模块或 Pack 定义。</summary>
        protected override void RegisterCustomerTypes(IContainerRegistry r)
        {
            //r.RegisterSingleton<DM.Process.Services.IForceSource, MyPressureForceSource>();   // 力传感器（见第 3 部分）
            //r.RegisterSingleton<IPowerMeter, MyPowerMeter>();                                  // 功率计
        }

     
    }
}

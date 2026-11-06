using DM.Audit.Module;
using DM.Core.Audit;
using DM.Core.Common;
using DM.Core.Devices;
using DM.Core.Logging;
using DM.Core.Projects;
using DM.Engine.Hosting;
using DM.Identity.Module;
using DM.Log;
using DM.Scripting.Commands;
using DM.Scripting.UI.Module;
using DM.Shell.Properties;
using DM.Shell.ViewModels;
using DM.Shell.Views;
using DM.Workbench.Module;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DM.Shell
{
    /// <summary>
    /// 宿主基类。固定流程：单实例 → 注册 → 加载模块 → 登记扩展点 → 登录 → 创建并显示主窗口 → 后台连设备 → 打开上次项目。
    /// 客户宿主只重写下面几个成员。主窗口在模块都初始化之后才创建（见文件顶部说明）。
    /// </summary>
    public abstract class DmApp : PrismApplication
    {
        private static Mutex _mutex;
        private DateTime _lastErrorBox = DateTime.MinValue;

        protected DmApp() { ShutdownMode = ShutdownMode.OnExplicitShutdown; }

        protected abstract AppPaths CreatePaths();
        protected abstract void ConfigureCustomerModules(IModuleCatalog catalog);
        protected virtual void RegisterCustomerTypes(IContainerRegistry registry) { }
        protected virtual string AppTitle => "设备控制系统";
        protected virtual Type ShellType => typeof(MainWindow);

        protected override void OnStartup(StartupEventArgs e)
        {
            bool created;
            _mutex = new Mutex(true, "Global\\DM." + GetType().Assembly.GetName().Name, out created);
            if (!created) { MessageBox.Show("程序已经在运行！", "提示"); Environment.Exit(0); return; }

            InstallExceptionHandlers();
            EnsureSharedResources();
            base.OnStartup(e);
        }

        protected override void RegisterTypes(IContainerRegistry r)
        {
            AppPaths paths = CreatePaths();
            paths.EnsureDirectories();
            r.RegisterInstance(paths);
            r.RegisterInstance(new ShellOptions(AppTitle));
            r.RegisterSingleton<LoginFlow>();
            r.RegisterDialog<AuditViewDialog, AuditViewDialogViewModel>("AuditViewDialog");
            RegisterCustomerTypes(r);
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog c)
        {
            c.AddModule<EngineModule>();            // 必须第一个：注册 AttachQueue
            c.AddModule<LogModule>();               // 靠前，启动日志不会丢
            c.AddModule<AuditModule>();
            c.AddModule<IdentityModule>();
            c.AddModule<ScriptingModule>();
            c.AddModule<ScriptingUiModule>();
            c.AddModule<WorkbenchModule>();
            ConfigureCustomerModules(c);
        }

        /// <summary>返回 null：主窗口等模块都初始化完再创建。</summary>
        protected override Window CreateShell() { return null; }

        protected override void OnInitialized()
        {
            try
            {
                var failures = Container.Resolve<AttachQueue>().RunAll();            // 此时所有模块的类型都已注册
                foreach (var f in failures) SafeLog("模块初始化失败：" + f.Key, f.Value);
                if (failures.Count > 0)
                    MessageBox.Show("以下模块初始化失败，相关功能不可用，详见日志：\n\n"
                        + string.Join("\n", failures.Select(f => f.Key + "：" + f.Value.Message)), "警告", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (!Container.Resolve<LoginFlow>().Run()) { ShellExit.Run(Container); return; }
                StartShell();
            }
            catch (Exception ex)
            {
                SafeLog("启动失败", ex);
                MessageBox.Show("启动失败：" + ex.Message, "错误");
                Environment.Exit(1);
            }
        }

        private void StartShell()
        {
            var shell = (Window)Container.Resolve(ShellType);
            RegionManager.SetRegionManager(shell, Container.Resolve<IRegionManager>());
            RegionManager.UpdateRegions();
            MainWindow = shell;
            shell.Show();

            SessionEnding += (s, e) => ShellExit.Run(Container);                    // Windows 注销、关机：尽力保存
            Container.Resolve<IDeviceHub>().ConnectAllInBackground();               // 后台连设备，不阻塞界面
            OpenLastProject();
        }

        private async void OpenLastProject()
        {
            try
            {
                ProjectInfo last = Container.Resolve<ILastProjectStore>().Read();    // 没有、损坏、目录已不存在都返回 null
                if (last == null) return;

                ProjectOpenResult res = await Container.Resolve<IProjectLifecycle>().OpenAsync(last.Name, last.Directory);
                if (res.Aborted)
                    MessageBox.Show("上次项目打开失败：\n" + FailText(res), "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                else if (res.HasWarnings)
                    MessageBox.Show("项目已打开，但部分模块失败：\n" + FailText(res), "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { SafeLog("打开上次项目失败", ex); }
        }

        private static string FailText(ProjectOpenResult r) { return string.Join("\n", r.Failures.Select(f => f.Name + "：" + f.Error.Message)); }

        // ───── 公共资源：Fields.xaml 里有编辑界面用到的 Field 样式，各模块的编辑界面都依赖它 ─────
        private void EnsureSharedResources()
        {
            const string fields = "pack://application:,,,/DM.UI;component/Themes/Fields.xaml";
            if (Resources.MergedDictionaries.Any(d => d.Source != null && d.Source.OriginalString == fields)) return;
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(fields, UriKind.Absolute) });
        }

        // ───── 全局异常：记录、记审计、提示（5 秒内只弹一次），程序继续运行 ─────
        private void InstallExceptionHandlers()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                SafeLog("界面线程未处理异常", e.Exception);
                if (DateTime.UtcNow - _lastErrorBox > TimeSpan.FromSeconds(5))
                {
                    _lastErrorBox = DateTime.UtcNow;
                    MessageBox.Show("发生未处理的错误：" + e.Exception.Message + "\n详情见日志。", "错误");
                }
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => SafeLog("未处理异常（程序可能退出）", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) => { SafeLog("未观察到的任务异常", e.Exception); e.SetObserved(); };
        }

        private void SafeLog(string message, Exception ex)
        {
            try { Container.Resolve<IAppLogger>().Error(message, ex); }
            catch (Exception) { System.Diagnostics.Debug.WriteLine(message + "：" + ex); }
            try { Container.Resolve<IAuditService>().Record(AuditCategory.System, "UnhandledException", null, result: AuditResult.Failed, detail: ex == null ? message : message + "：" + ex.Message); }
            catch (Exception) { }
        }
    }
}

using DM.Core.Actions;
using DM.Core.Common;
using DM.Core.Data;
using DM.Core.Devices;
using DM.Core.Projects;
using DM.Core.TaskList;
using DM.Engine.Actions;
using DM.Engine.Common;
using DM.Engine.Config;
using DM.Engine.Data;
using DM.Engine.Devices;
using DM.Engine.Projects;
using DM.Engine.TaskList;
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Engine.Hosting
{
    /// <summary>
    /// 功能模块基类。规则只有一条：Register 里只注册类型，不解析任何东西。
    /// 登记扩展点（Attach）被推迟到所有模块都注册完之后，由宿主统一执行，所以模块的加载顺序不影响正确性。
    /// </summary>
    public abstract class ModuleBase : IModule //所有模块的基类
    {
     
        public void RegisterTypes(IContainerRegistry registry) 
        { 
            Register(registry); 
        }

        public void OnInitialized(IContainerProvider provider)
        {
            provider.Resolve<AttachQueue>().Add(GetType().Name, () => Attach(new ModuleScope(provider)));
        }

        protected abstract void Register(IContainerRegistry registry);
        protected virtual void Attach(ModuleScope scope) { }
    }

    public sealed class AttachQueue
    {
        private readonly List<KeyValuePair<string, Action>> _items = new List<KeyValuePair<string, Action>>();
        private readonly object _g = new object();

        public void Add(string module, Action attach)
        { lock (_g) _items.Add(new KeyValuePair<string, Action>(module, attach)); }

        /// <summary>按模块加载顺序执行；某个模块失败不影响其他，返回所有失败项。</summary>
        public List<KeyValuePair<string, Exception>> RunAll()
        {
            List<KeyValuePair<string, Action>> list;

            lock (_g) 
            { 
                list = _items.ToList(); 
                _items.Clear(); 
            }
            var failures = new List<KeyValuePair<string, Exception>>();
            foreach (var it in list)
            {
                try 
                { 
                    it.Value(); 
                }
                catch (Exception ex) 
                { 
                    failures.Add(new KeyValuePair<string, Exception>(it.Key, ex));
                }
            }
            return failures;
        }
    }

    /// <summary>模块可以向平台贡献的扩展点。脚本命令由 DM.Scripting 提供扩展方法 Command&lt;T&gt;()。</summary>
    public sealed class ModuleScope
    {
        private readonly IContainerProvider _p;
        public ModuleScope(IContainerProvider p) 
        { 
            _p = p; 
        }

        public T Resolve<T>() 
        { 
            return _p.Resolve<T>(); 
        }

        public ModuleScope Device<T>() where T : IDeviceSession
        { 
            _p.Resolve<IDeviceHub>().Register(_p.Resolve<T>()); 
            return this; 
        }

        public ModuleScope ProjectPart<T>() where T : IProjectAware
        { 
            _p.Resolve<IProjectAwareRegistry>().Register(_p.Resolve<T>()); 
            return this; 
        }

        public ModuleScope TaskType<T>() where T : ITaskType
        { 
            _p.Resolve<ITaskTypeRegistry>().Register(_p.Resolve<T>()); 
            return this; 
        }

        public ModuleScope Seeder<T>() where T : IParameterSeeder
        { 
            _p.Resolve<IParameterSeederRegistry>().Register(_p.Resolve<T>()); 
            return this; 
        }

        /// <summary>创建即生效的对象（如订阅事件的服务）。</summary>
        public ModuleScope Eager<T>() where T : class 
        { 
            _p.Resolve<T>(); 
            return this; 
        }
    }

    /// <summary>退出流程：停任务 → 关闭项目（含保存）→ 限时断开设备 → 强制结束进程。全程在线程池，Interlocked 防重入。</summary>
    public static class AppShutdown
    {
        private static int _started;

        public static void Run(IContainerProvider c, Action<string> saveLastProject)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1) return;
            Task.Run(async () =>
            {
                try
                {
                    c.Resolve<ITaskRunner>().Stop("程序退出");
                    var life = c.Resolve<IProjectLifecycle>();
                    string name = life.Current == null ? null : life.Current.Name;
                    if (name != null) saveLastProject(name);
                    await Task.WhenAny(life.CloseAsync(), Task.Delay(2000)).ConfigureAwait(false);
                    await c.Resolve<IDeviceHub>().DisconnectAllAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch { /* 退出路径不抛 */ }
                finally { Environment.Exit(0); }
            });
        }
    }

    /// <summary>
    /// 引擎模块，必须第一个加载。外部需要提供：AppPaths（宿主）、IAppLogger（DM.Log）、
    /// IAuditService（DM.Audit）、ICurrentUser（DM.Identity）。
    /// </summary>
    public sealed class EngineModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<AttachQueue>();
            r.RegisterSingleton<IClock, SystemClock>();
            r.RegisterSingleton<IMachineConfig, MachineConfig>();

            // 一个实现暴露多个接口时，用工厂指向同一个具体单例，保证是同一实例
            r.RegisterSingleton<DataContainer>();
            r.RegisterSingleton<IDataContainer>(c => c.Resolve<DataContainer>());
            r.RegisterSingleton<IParameterSeederRegistry, ParameterSeederRegistry>();
            r.RegisterSingleton<DataProjectPart>();

            r.RegisterSingleton<ProjectCatalog>();
            r.RegisterSingleton<IProjectCatalog>(c => c.Resolve<ProjectCatalog>());
            r.RegisterSingleton<ILastProjectStore>(c => c.Resolve<ProjectCatalog>());
            r.RegisterSingleton<ProjectLifecycleCoordinator>();
            r.RegisterSingleton<IProjectLifecycle>(c => c.Resolve<ProjectLifecycleCoordinator>());
            r.RegisterSingleton<IProjectAwareRegistry>(c => c.Resolve<ProjectLifecycleCoordinator>());

            r.RegisterSingleton<IDeviceHub, DeviceHub>();
            r.RegisterSingleton<ISafetyState, SafetyState>();
            r.RegisterSingleton<IActionGateway, ActionGateway>();

            r.RegisterSingleton<ITaskTypeRegistry, TaskTypeRegistry>();
            r.RegisterSingleton<ITaskRunner, TaskRunner>();
        }

        protected override void Attach(ModuleScope s)
        {
            s.Eager<ISafetyState>()                 // 必须早创建，否则会错过创建之前发出的急停
             .ProjectPart<DataProjectPart>();
        }
    }
}

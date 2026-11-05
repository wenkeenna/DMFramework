using DM.Audit.Services;
using DM.Core.Audit;
using DM.Core.Common;
using DM.Engine.Hosting;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Audit.Module
{
    /// <summary>生产环境必装。单元测试里用 NullAuditService 代替。</summary>
    public sealed class AuditModule : ModuleBase
    {
        protected override void Register(IContainerRegistry r)
        {
            r.RegisterSingleton<IAuditStore>(c => new SqliteAuditStore(c.Resolve<AppPaths>().AuditDb));
            r.RegisterSingleton<AuditService>();
            r.RegisterSingleton<IAuditService>(c => c.Resolve<AuditService>());      // 与设备中心拿到同一实例
            r.RegisterSingleton<IAuditReader, AuditReader>();
            r.RegisterSingleton<DataAuditBridge>();
        }

        protected override void Attach(ModuleScope s)
        {
            s.Device<AuditService>()              // 退出刷盘
             .Eager<DataAuditBridge>();           // 创建即订阅

            Version v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            s.Resolve<IAuditService>().Record(AuditCategory.System, "Startup", null, detail: v == null ? null : "v" + v);
        }
    }
}

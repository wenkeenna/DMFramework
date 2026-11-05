using DM.Core.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Core.Actions
{
    public interface ISafetyState { bool IsEmergencyStopped { get; } string Reason { get; } }

    /// <summary>所有硬件动作的统一入口：任务、脚本命令、手动界面都经过这里，联锁和留痕不会被漏掉。</summary>
    public interface IActionGateway
    {
        /// <summary>自动运行来源（任务、脚本命令）：只检查联锁，运行轨迹由任务级审计和日志记录。</summary>
        Task RunAutoAsync(string action, string target, Func<Task> body);

        /// <summary>手动来源：检查权限和联锁，成功、失败、拒绝都写审计。</summary>
        Task RunManualAsync(string action, string target, AccessRole required, Func<Task> body, string detail = null);
    }
}

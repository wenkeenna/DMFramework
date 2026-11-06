using DM.Core.Projects;
using DM.Engine.Hosting;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Shell
{
    /// <summary>
    /// 统一退出入口：停任务 → 关闭项目 → 限时断开设备 → 结束进程。
    /// "记住上次项目"的写入包了 try/catch：AppShutdown 里这个回调若抛异常，后面的"关闭项目并保存"会被整体跳过。
    /// </summary>
    public static class ShellExit
    {
        public static void Run(IContainerProvider c)
        {
            ILastProjectStore last = null;
            try { last = c.Resolve<ILastProjectStore>(); } catch (Exception) { }
            AppShutdown.Run(c, name => { try { if (last != null) last.Write(name); } catch (Exception) { } });
        }
    }
}

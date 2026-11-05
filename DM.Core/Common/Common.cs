using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Core.Common
{
    public interface IClock { DateTime UtcNow { get; } }

    /// <summary>读取 machine.json 的某一段；段不存在返回默认对象，由使用方校验。</summary>
    public interface IMachineConfig { T Get<T>(string section) where T : class, new(); }

    /// <summary>所有落盘位置的唯一来源，宿主只给一个根目录。</summary>
    public sealed class AppPaths
    {
        public AppPaths(string root) { Root = root; }
        public string Root { get; }
        public string Config => Path.Combine(Root, "Config");
        public string Logs => Path.Combine(Root, "Logs");
        public string Projects => Path.Combine(Root, "Projects");
        public string AuditDb => Path.Combine(Logs, "audit.db");
        public string IdentityDb => Path.Combine(Config, "accounts.db");

        public void EnsureDirectories()
        { foreach (var d in new[] { Config, Logs, Projects }) Directory.CreateDirectory(d); }
    }

    /// <summary>一次任务执行的上下文：取服务 + 写日志。直接用 Prism 容器，不另造抽象。</summary>
    public interface IRunContext
    {
        IContainerProvider Container { get; }
        Guid RunId { get; }
        string TaskName { get; }
        void Log(string message);
    }

    public static class RunContextExtensions
    {
        public static T Get<T>(this IRunContext ctx) where T : class
        {
            try { return (T)ctx.Container.Resolve(typeof(T)); }
            catch (Exception ex)
            { throw new InvalidOperationException("无法获取服务 " + typeof(T).Name + "：" + ex.Message, ex); }
        }
    }
}

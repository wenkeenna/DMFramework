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
        public string AuditDb => Path.Combine(Config, "audit.db");
        public string IdentityDb => Path.Combine(Config, "accounts.db");

        //public void EnsureDirectories()
        //{ 
        //    foreach (var d in new[] { Config, Logs, Projects })
        //        CreateDir(d); 
        //}

        /// <summary>
        /// 创建并验证所有落盘目录。每个目录都做一次写入测试：只创建不够，目录已存在但不可写
        /// （例如旧程序以管理员身份创建的）要到运行时才会暴露。失败抛 InvalidOperationException，消息里带具体路径。
        /// </summary>
        public void EnsureDirectories()
        {
            foreach (string d in new[] { Root, Config, Logs, Projects })
            {
                try { Directory.CreateDirectory(d); }
                catch (Exception ex)
                { throw new InvalidOperationException("无法创建目录：" + d + "\n原因：" + ex.Message, ex); }

                string probe = Path.Combine(d, ".write-test-" + Guid.NewGuid().ToString("N"));
                try { File.WriteAllText(probe, "x"); File.Delete(probe); }
                catch (Exception ex)
                { throw new InvalidOperationException("目录不可写：" + d + "\n原因：" + ex.Message, ex); }
            }
        }

        /// <summary>启动自检：把身份和四个候选目录的可写性一次性列出来。</summary>
        public static string Diagnose()
        {
            var sb = new StringBuilder();
            var id = System.Security.Principal.WindowsIdentity.GetCurrent();

            sb.AppendLine("当前用户  ：" + id.Name);
            sb.AppendLine("管理员    ：" + new System.Security.Principal.WindowsPrincipal(id)
                              .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));
            sb.AppendLine("进程位数  ：" + (IntPtr.Size == 8 ? "64 位" : "32 位"));
            sb.AppendLine("工作目录  ：" + Directory.GetCurrentDirectory());
            sb.AppendLine("----------------------------------------");
            sb.AppendLine(RunWhoAmI());
            string[] candidates =
            {
        @"D:\MyApp-Test",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DMFramework"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DMFramework"),
        Path.Combine(Path.GetTempPath(), "DMFramework")
    };

            foreach (string p in candidates)
            {
                bool existed = Directory.Exists(p);
                try
                {
                    Directory.CreateDirectory(p);
                    string probe = Path.Combine(p, ".probe");
                    File.WriteAllText(probe, "x");
                    File.Delete(probe);
                    if (!existed) Directory.Delete(p, true);          // 只清理自己新建的，不动已有数据
                    sb.AppendLine("✓ 可写   " + p);
                }
                catch (Exception ex)
                {
                    sb.AppendLine("✗ 不可写 " + p);
                    sb.AppendLine("        " + ex.GetType().Name + "：" + ex.Message);
                }
            }
            return sb.ToString();
        }

        private static string RunWhoAmI()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("whoami", "/groups")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    return outp;
                }
            }
            catch (Exception ex) { return "whoami 调用失败：" + ex.Message; }
        }
        private static void CreateDir(string dir)
        {
            try 
            { 
                Directory.CreateDirectory(dir); 
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "无法创建数据目录：" + dir + Environment.NewLine +
                    "当前用户：" + System.Security.Principal.WindowsIdentity.GetCurrent().Name + Environment.NewLine +
                    "常见原因：① 该位置需要管理员权限 ② 磁盘只读/被安全软件拦截 ③ 路径非法" + Environment.NewLine +
                    "解决办法：把数据根目录改到 %ProgramData%\\DMFramework，或用管理员身份运行。" + Environment.NewLine +
                    "原始错误：" + ex.GetType().Name + "：" + ex.Message, ex);
            }
        }
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
            try 
            { 
                return (T)ctx.Container.Resolve(typeof(T)); 
            }
            catch (Exception ex)
            { 
                throw new InvalidOperationException("无法获取服务 " + typeof(T).Name + "：" + ex.Message, ex); 
            }
        }
    }
}

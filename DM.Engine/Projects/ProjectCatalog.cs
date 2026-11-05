using DM.Core.Audit;
using DM.Core.Common;
using DM.Core.Projects;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.Projects
{
    /// <summary>项目 = 根目录下含 project.json 的子目录。名称校验防止路径穿越和 Windows 保留名。</summary>
    public sealed class ProjectCatalog : IProjectCatalog, ILastProjectStore
    {
        private static readonly string[] Reserved =
        { "CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
          "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9" };

        private readonly string _root;
        private readonly IAuditService _audit;

        public ProjectCatalog(AppPaths paths, IAuditService audit)
        { _root = paths.Projects; _audit = audit; Directory.CreateDirectory(_root); }

        public static string CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "项目名称不能为空。";
            if (name != name.Trim()) return "项目名称首尾不能有空格。";
            if (name.Length > 64) return "项目名称不能超过 64 个字符。";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "项目名称不能包含 \\ / : * ? \" < > | 等字符。";
            if (name.StartsWith(".") || name.EndsWith(".")) return "项目名称不能以点开头或结尾。";
            if (Reserved.Contains(name.ToUpperInvariant())) return "项目名称是系统保留名：" + name;
            return null;
        }

        public ProjectInfo Find(string name)
        {
            if (CheckName(name) != null) return null;
            string dir = Path.Combine(_root, name), meta = Path.Combine(dir, "project.json");
            if (!File.Exists(meta)) return null;
            DateTime created = Directory.GetCreationTimeUtc(dir);
            try { created = (DateTime)JObject.Parse(File.ReadAllText(meta))["CreatedUtc"]; } catch (Exception) { }
            return new ProjectInfo { Name = name, Directory = dir, CreatedUtc = created };
        }

        public IReadOnlyList<ProjectInfo> List()
        {
            return Directory.GetDirectories(_root).Select(d => Find(Path.GetFileName(d)))
                .Where(p => p != null).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public ProjectInfo Create(string name, out string error)
        {
            error = CheckName(name);
            if (error != null) return null;
            string dir = Path.Combine(_root, name);
            if (Directory.Exists(dir)) { error = "项目已存在：" + name; return null; }

            Directory.CreateDirectory(dir);
            var meta = new JObject { ["Version"] = 1, ["Name"] = name, ["CreatedUtc"] = DateTime.UtcNow.ToString("o") };
            File.WriteAllText(Path.Combine(dir, "project.json"), meta.ToString(), new UTF8Encoding(false));
            _audit.Record(AuditCategory.Project, "Create", name);
            return Find(name);
        }

        // ───── ILastProjectStore ─────
        private string LastFile => Path.Combine(_root, "last-project.json");

        public ProjectInfo Read()
        {
            try
            {
                if (!File.Exists(LastFile)) return null;
                return Find((string)JObject.Parse(File.ReadAllText(LastFile))["Name"]);
            }
            catch (Exception) { return null; }          // 记录损坏当作没有，不影响启动
        }

        public void Write(string name)
        {
            string tmp = LastFile + ".tmp";
            File.WriteAllText(tmp, new JObject { ["Name"] = name }.ToString(), new UTF8Encoding(false));
            if (File.Exists(LastFile)) File.Replace(tmp, LastFile, null); else File.Move(tmp, LastFile);
        }

        public void Clear() { if (File.Exists(LastFile)) File.Delete(LastFile); }
    }
}

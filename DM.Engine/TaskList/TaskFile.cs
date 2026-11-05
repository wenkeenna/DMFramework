using DM.Core.TaskList;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.TaskList
{
    /// <summary>tasks.json 读写。缺失 = 空列表；格式错误抛 InvalidOperationException（调用方据此禁止保存，防止覆盖）。</summary>
    public static class TaskFile
    {
        public const string FileName = "tasks.json";

        private sealed class Doc
        {
            public int Version { get; set; } = 1;
            public List<TaskItem> Tasks { get; set; } = new List<TaskItem>();
        }

        public static List<TaskItem> Load(string path)
        {
            if (!File.Exists(path)) return new List<TaskItem>();
            Doc d;
            try { d = JsonConvert.DeserializeObject<Doc>(File.ReadAllText(path, Encoding.UTF8)); }
            catch (JsonException ex) { throw new InvalidOperationException("任务文件格式错误（" + path + "）：" + ex.Message, ex); }
            if (d == null) throw new InvalidOperationException("任务文件为空或损坏：" + path);
            if (d.Version > 1) throw new InvalidOperationException("任务文件版本 " + d.Version + " 高于本程序支持的版本。");
            return d.Tasks.Where(t => t != null).ToList();
        }

        public static void Save(string path, IEnumerable<TaskItem> tasks)
        {
            string json = JsonConvert.SerializeObject(new Doc { Tasks = tasks.ToList() }, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }
    }
}

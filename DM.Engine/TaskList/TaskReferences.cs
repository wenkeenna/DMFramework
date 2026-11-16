using DM.Core.TaskList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DM.Engine.TaskList
{
    /// <summary>
    /// 在任务数据里查找对某个参数键（或整组前缀 "Root."）的引用。
    /// 按"整词"匹配：键前后不能紧挨着字母、数字、下划线、点、中文，避免 Pos.PickZ 误中 Pos.PickZ2、XPos.PickZ。
    /// 不依赖任务类型的内部结构，扩展包的新任务类型自动支持。
    /// </summary>
    public static class TaskReferences
    {
        /// <param name="isGroup">true 时 key 是组名，匹配 "组名." 开头的任意键。</param>
        public static List<string> Find(IEnumerable<TaskItem> tasks, string key, bool isGroup)
        {
            string pattern = isGroup
                ? @"(?<![\w.])" + Regex.Escape(key) + @"\.[^\s""',;:)\]}]+"
                : @"(?<![\w.])" + Regex.Escape(key) + @"(?![\w.])";
            var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return tasks.Where(t => t != null && !string.IsNullOrEmpty(t.Data) && rx.IsMatch(t.Data))
                        .Select(t => string.IsNullOrWhiteSpace(t.Name) ? t.Id : t.Name).ToList();
        }
    }
}

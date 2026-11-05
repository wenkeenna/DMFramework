using DM.Core.Common;
using DM.Core.Scripting;
using DM.Core.TaskList;
using DM.Scripting.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Scripting.Commands
{
    /// <summary>脚本任务：任务系统里一个普通的任务类型，Data 就是脚本文本。</summary>
    public sealed class ScriptTaskType : ITaskType
    {
        public const string TypeKey = "script";
        private readonly IScriptCommandRegistry _reg;
        public ScriptTaskType(IScriptCommandRegistry reg) { _reg = reg; }

        public string Key => TypeKey;
        public string DisplayName => "脚本任务";
        public string Group => "通用";
        public string EditorView => "ScriptEditorView";         // 只是视图名字符串，不依赖 UI 项目

        public string CreateDefaultData() { return "# 在这里编写脚本\r\n"; }
        public string Validate(string data) { return ScriptExec.Check(_reg, data ?? ""); }
        public Task ExecuteAsync(string data, IRunContext ctx, CancellationToken ct) { return ScriptExec.RunAsync(_reg, data ?? "", ctx, ct); }

        public string Summary(string data)
        {
            var lines = (data ?? "").Split('\n').Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#") && !l.StartsWith("//")).ToList();
            return lines.Count == 0 ? "（空脚本）" : lines[0] + (lines.Count > 1 ? "  … 共 " + lines.Count + " 行" : "");
        }
    }
}

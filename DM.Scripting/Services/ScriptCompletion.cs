using DM.Core.Data;
using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Scripting.Services
{
    public sealed class CompletionItem { public string Text; public string Description; }

    /// <summary>
    /// 纯逻辑：给定"光标前的当前行文本"，返回候选项。界面只负责弹窗。
    /// 第一个词 → 命令名；之后按位置取对应参数：Options / Suggest / 布尔(ON,OFF) / 以 @ 开头则列数据键。
    /// </summary>
    public sealed class ScriptCompletion
    {
        private readonly IScriptCommandRegistry _reg;
        private readonly IDataContainer _data;
        public ScriptCompletion(IScriptCommandRegistry reg, IDataContainer data) { _reg = reg; _data = data; }

        /// <param name="replaceLength">正在输入的那个词的长度，界面据此确定替换范围。</param>
        public IReadOnlyList<CompletionItem> GetItems(string linePrefix, out int replaceLength)
        {
            replaceLength = 0;
            string text = (linePrefix ?? "").TrimStart();
            if (text.StartsWith("#") || text.StartsWith("//")) return new CompletionItem[0];

            bool endsWithSpace = text.Length > 0 && char.IsWhiteSpace(text[text.Length - 1]);
            string[] tokens = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string partial = endsWithSpace || tokens.Length == 0 ? "" : tokens[tokens.Length - 1];
            replaceLength = partial.Length;
            int paramIndex = (endsWithSpace ? tokens.Length : tokens.Length - 1) - 1;      // -1 表示在写命令名

            if (paramIndex < 0)
                return _reg.All.Where(c => c.Spec.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Spec.Name)
                    .Select(c => new CompletionItem { Text = c.Spec.Name, Description = c.Spec.HelpText + "\n" + c.Spec.Description }).ToList();

            IScriptCommand cmd;
            if (!_reg.TryGet(tokens[0], out cmd)) return new CompletionItem[0];
            ScriptParamSpec[] ps = cmd.Spec.Params;
            if (ps.Length == 0) return new CompletionItem[0];

            ScriptParamSpec p = paramIndex < ps.Length ? ps[paramIndex] : (ps[ps.Length - 1].TakesRest ? ps[ps.Length - 1] : null);
            if (p == null) return new CompletionItem[0];

            IEnumerable<string> cands;
            if (partial.StartsWith("@") && p.AllowBinding)
                cands = new[] { DataScope.Project, DataScope.Machine, DataScope.Runtime }
                        .SelectMany(s => _data.Snapshot(s)).Select(kv => "@" + kv.Key).Distinct();
            else
            {
                var l = new List<string>();
                if (p.Options != null) l.AddRange(p.Options);
                if (p.Suggest != null) { try { l.AddRange(p.Suggest()); } catch (Exception) { } }
                if (p.Type == typeof(bool)) { l.Add("ON"); l.Add("OFF"); }
                cands = l;
            }

            string desc = p.Name + (string.IsNullOrEmpty(p.Description) ? "" : "：" + p.Description);
            return cands.Where(x => x.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x).Select(x => new CompletionItem { Text = x, Description = desc }).ToList();
        }
    }
}

using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DM.Scripting.Services
{
    /// <summary>生成 AvalonEdit 的 XSHD 文本。命令名、选项值全部来自注册表，新增命令自动高亮。</summary>
    public static class ScriptHighlightingBuilder
    {
        private static readonly Regex WordRx = new Regex(@"^[A-Za-z0-9_]+$");

        public static string BuildXshd(IScriptCommandRegistry reg)
        {
            var cmds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sw = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ON", "OFF", "TRUE", "FALSE" };

            foreach (IScriptCommand c in reg.All)
            {
                if (WordRx.IsMatch(c.Spec.Name)) cmds.Add(c.Spec.Name);
                foreach (ScriptParamSpec p in c.Spec.Params)
                    if (p.Options != null) foreach (string o in p.Options) if (o.Length > 1 && WordRx.IsMatch(o)) sw.Add(o);
            }

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<SyntaxDefinition name=\"Script\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">");
            sb.Append("<Color name=\"Command\" foreground=\"#1565C0\" fontWeight=\"bold\"/>");
            sb.Append("<Color name=\"Switch\" foreground=\"#2E7D32\" fontWeight=\"bold\"/>");
            sb.Append("<Color name=\"Comment\" foreground=\"#808080\" fontStyle=\"italic\"/>");
            sb.Append("<Color name=\"String\" foreground=\"#C62828\"/>");
            sb.Append("<Color name=\"Number\" foreground=\"#6A1B9A\"/>");
            sb.Append("<Color name=\"Binding\" foreground=\"#E67E22\"/>");
            sb.Append("<RuleSet ignoreCase=\"true\">");
            sb.Append("<Span color=\"Comment\" begin=\"#\" end=\"\\n\"/>");
            sb.Append("<Span color=\"Comment\" begin=\"//\" end=\"\\n\"/>");
            sb.Append("<Span color=\"String\" begin=\"&quot;\" end=\"&quot;\"/>");
            Words(sb, "Command", cmds);
            Words(sb, "Switch", sw);
            sb.Append("<Rule color=\"Number\">-?\\b\\d+(\\.\\d+)?\\b</Rule>");
            sb.Append("<Rule color=\"Binding\">@[^\\s]+</Rule>");
            sb.Append("</RuleSet></SyntaxDefinition>");
            return sb.ToString();
        }

        private static void Words(StringBuilder sb, string color, IEnumerable<string> words)
        {
            var list = words.OrderBy(x => x).ToList();
            if (list.Count == 0) return;            // 空的 Keywords 元素会让加载失败
            sb.Append("<Keywords color=\"" + color + "\">");
            foreach (string w in list) sb.Append("<Word>" + w + "</Word>");
            sb.Append("</Keywords>");
        }
    }
}

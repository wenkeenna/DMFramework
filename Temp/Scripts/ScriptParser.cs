using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.Scripts
{
    public sealed class ScriptError
    {
        public ScriptError(int line, string message) { Line = line; Message = message; }
        public int Line { get; }
        public string Message { get; }
    }

    public sealed class ScriptToken { public string Text; public bool Quoted; }

    public sealed class ScriptLine
    {
        public int LineNumber;
        public string Raw;
        public string Name;
        public List<ScriptToken> Args = new List<ScriptToken>();
    }

    /// <summary>语法：每行一条命令，空格分隔参数，双引号包裹含空格的值，# 或 // 开头为注释。</summary>
    public static class ScriptParser
    {
        public static List<ScriptLine> Parse(string text, List<ScriptError> errors)
        {
            var result = new List<ScriptLine>();
            string[] lines = (text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;

                string err;
                List<ScriptToken> tokens = Tokenize(line, out err);
                if (err != null) { errors.Add(new ScriptError(i + 1, err)); continue; }
                if (tokens.Count == 0) continue;

                var sl = new ScriptLine { LineNumber = i + 1, Raw = raw, Name = tokens[0].Text };
                sl.Args.AddRange(tokens.GetRange(1, tokens.Count - 1));
                result.Add(sl);
            }
            return result;
        }

        private static List<ScriptToken> Tokenize(string line, out string error)
        {
            error = null;
            var list = new List<ScriptToken>();
            var sb = new StringBuilder();
            bool inQuote = false, has = false, quoted = false;

            foreach (char ch in line)
            {
                if (ch == '"') { inQuote = !inQuote; has = true; quoted = true; continue; }
                if (!inQuote && char.IsWhiteSpace(ch))
                {
                    if (has) { list.Add(new ScriptToken { Text = sb.ToString(), Quoted = quoted }); sb.Clear(); has = false; quoted = false; }
                    continue;
                }
                sb.Append(ch); has = true;
            }
            if (inQuote) { error = "存在未闭合的引号。"; return list; }
            if (has) list.Add(new ScriptToken { Text = sb.ToString(), Quoted = quoted });
            return list;
        }
    }
}

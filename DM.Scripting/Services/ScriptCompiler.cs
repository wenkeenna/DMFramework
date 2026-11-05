using DM.Core.Data;
using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Scripting.Services
{
    public sealed class ScriptValue
    {
        public object Literal;
        public string BindKey;                          // 非空表示运行时从数据容器读取
        public bool IsBinding => BindKey != null;
    }

    public sealed class CompiledLine
    {
        public int LineNumber;
        public string Raw;
        public IScriptCommand Command;
        public Dictionary<string, ScriptValue> Values;
    }

    public sealed class CompileResult
    {
        public List<CompiledLine> Lines = new List<CompiledLine>();
        public List<ScriptError> Errors = new List<ScriptError>();
        public bool Ok => Errors.Count == 0;

        public string ErrorText(int max = 8)
        {
            var parts = Errors.Take(max).Select(e => "第 " + e.Line + " 行：" + e.Message).ToList();
            if (Errors.Count > max) parts.Add("……另有 " + (Errors.Count - max) + " 处错误");
            return string.Join("\n", parts);
        }
    }

    /// <summary>静态校验：不碰硬件，不读数据容器；一次收集所有错误。编辑器和运行前都调用。</summary>
    public sealed class ScriptCompiler
    {
        private readonly IScriptCommandRegistry _registry;
        public ScriptCompiler(IScriptCommandRegistry registry) { _registry = registry; }

        public CompileResult Compile(string text)
        {
            var r = new CompileResult();

            foreach (ScriptLine line in ScriptParser.Parse(text, r.Errors))
            {
                IScriptCommand cmd;
                if (!_registry.TryGet(line.Name, out cmd)) { r.Errors.Add(new ScriptError(line.LineNumber, "未知命令：" + line.Name)); continue; }

                ScriptParamSpec[] ps = cmd.Spec.Params;
                bool rest = ps.Length > 0 && ps[ps.Length - 1].TakesRest;
                int required = ps.Count(p => p.Required);
                if (line.Args.Count < required || (!rest && line.Args.Count > ps.Length))
                { r.Errors.Add(new ScriptError(line.LineNumber, "参数个数不对，格式：" + cmd.Spec.HelpText)); continue; }

                var values = new Dictionary<string, ScriptValue>(StringComparer.OrdinalIgnoreCase);
                bool ok = true;

                for (int i = 0; i < ps.Length; i++)
                {
                    ScriptParamSpec p = ps[i];
                    if (i >= line.Args.Count) { values[p.Name] = new ScriptValue { Literal = p.Default }; continue; }

                    ScriptToken tok = line.Args[i];
                    if (rest && i == ps.Length - 1)         // 最后一个参数吞掉剩余所有词
                        tok = new ScriptToken { Text = string.Join(" ", line.Args.Skip(i).Select(t => t.Text)), Quoted = true };

                    string err;
                    ScriptValue v = ConvertToken(p, tok, out err);
                    if (err != null) { r.Errors.Add(new ScriptError(line.LineNumber, p.Name + "：" + err)); ok = false; continue; }
                    values[p.Name] = v;
                }
                if (!ok) continue;

                var check = cmd as IScriptCommandCheck;
                if (check != null)
                {
                    var literals = values.Where(kv => !kv.Value.IsBinding)
                                         .ToDictionary(kv => kv.Key, kv => kv.Value.Literal, StringComparer.OrdinalIgnoreCase);
                    string msg = check.Check(literals);
                    if (msg != null) { r.Errors.Add(new ScriptError(line.LineNumber, msg)); continue; }
                }

                r.Lines.Add(new CompiledLine { LineNumber = line.LineNumber, Raw = line.Raw, Command = cmd, Values = values });
            }
            return r;
        }

        private static ScriptValue ConvertToken(ScriptParamSpec p, ScriptToken t, out string error)
        {
            error = null;
            string r, c;

            if (p.IsDataKey)
            {
                if (!DataKey.TrySplit(t.Text, out r, out c)) { error = "应为 Root.Child 形式的数据键：" + t.Text; return null; }
                return new ScriptValue { Literal = t.Text };
            }

            // 带引号的 "@x" 当普通字符串；不带引号且参数允许绑定才当绑定
            if (!t.Quoted && p.AllowBinding && t.Text.StartsWith("@"))
            {
                string key = t.Text.Substring(1);
                if (!DataKey.TrySplit(key, out r, out c)) { error = "绑定格式应为 @Root.Child：" + t.Text; return null; }
                return new ScriptValue { BindKey = key };
            }

            object val = ScriptValueConverter.Parse(p, t.Text, out error);
            return error == null ? new ScriptValue { Literal = val } : null;
        }
    }
}

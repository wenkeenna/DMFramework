using DM.Core.Data;
using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.Scripts
{
    public sealed class ScriptValue
    {
        public object Literal;
        public string BindKey;                                   // 非空表示运行时从数据容器读取
        public bool IsBinding { get { return BindKey != null; } }
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
        public bool Ok { get { return Errors.Count == 0; } }

        public string ErrorText(int max = 8)
        {
            var parts = Errors.Take(max).Select(e => "第 " + e.Line + " 行：" + e.Message).ToList();
            if (Errors.Count > max) parts.Add("……另有 " + (Errors.Count - max) + " 处错误");
            return string.Join("\n", parts);
        }
    }

    /// <summary>静态校验：不碰硬件，不读数据容器；一次收集所有错误。编辑器保存时和运行前都会调用。</summary>
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
                if (!_registry.TryGet(line.Name, out cmd))
                { r.Errors.Add(new ScriptError(line.LineNumber, "未知命令：" + line.Name)); continue; }

                ScriptCommandSpec spec = cmd.Spec;
                int required = spec.Params.Count(p => p.Required);
                if (line.Args.Count < required || line.Args.Count > spec.Params.Length)
                { r.Errors.Add(new ScriptError(line.LineNumber, "参数个数不对，格式：" + spec.HelpText)); continue; }

                var values = new Dictionary<string, ScriptValue>(StringComparer.OrdinalIgnoreCase);
                bool ok = true;

                for (int i = 0; i < spec.Params.Length; i++)
                {
                    ScriptParamSpec p = spec.Params[i];
                    if (i >= line.Args.Count) { values[p.Name] = new ScriptValue { Literal = p.Default }; continue; }

                    string err;
                    ScriptValue v = ConvertToken(p, line.Args[i], out err);
                    if (err != null) { r.Errors.Add(new ScriptError(line.LineNumber, p.Name + "：" + err)); ok = false; continue; }
                    values[p.Name] = v;
                }

                if (ok) r.Lines.Add(new CompiledLine { LineNumber = line.LineNumber, Raw = line.Raw, Command = cmd, Values = values });
            }
            return r;
        }

        private static ScriptValue ConvertToken(ScriptParamSpec p, ScriptToken t, out string error)
        {
            error = null;

            // 带引号的 "@x" 当普通字符串；不带引号且参数允许绑定才当绑定
            if (!t.Quoted && p.AllowBinding && t.Text.StartsWith("@"))
            {
                string key = t.Text.Substring(1), root, child;
                if (!DataKey.TrySplit(key, out root, out child))
                { error = "绑定格式应为 @Root.Child：" + t.Text; return null; }
                return new ScriptValue { BindKey = key };
            }

            object val = ScriptValueConverter.Parse(p, t.Text, out error);
            return error == null ? new ScriptValue { Literal = val } : null;
        }
    }
}

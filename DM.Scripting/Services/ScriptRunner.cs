using DM.Core.Common;
using DM.Core.Data;
using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Scripting.Services
{
    /// <summary>顺序逐行执行。任何一行失败，整个任务失败（设备场景下继续往下跑很危险）。</summary>
    public static class ScriptRunner
    {
        public static async Task RunAsync(IReadOnlyList<CompiledLine> lines, IRunContext ctx, CancellationToken ct)
        {
            var data = ctx.Get<IDataContainer>();

            foreach (CompiledLine line in lines)
            {
                ct.ThrowIfCancellationRequested();
                var sw = Stopwatch.StartNew();
                ctx.Log("[" + line.LineNumber + "] " + line.Raw.Trim());
                try
                {
                    // 每行执行前才读取 @Root.Child：前面行写入的值对后面行生效
                    await line.Command.ExecuteAsync(Resolve(line, data), ctx, ct);
                    ctx.Log("[" + line.LineNumber + "] 完成 " + sw.ElapsedMilliseconds + "ms");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { throw new InvalidOperationException("第 " + line.LineNumber + " 行失败：" + ex.Message, ex); }
            }
        }

        private static ScriptArgs Resolve(CompiledLine line, IDataContainer data)
        {
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (ScriptParamSpec p in line.Command.Spec.Params)
            {
                ScriptValue sv = line.Values[p.Name];
                if (!sv.IsBinding) { d[p.Name] = sv.Literal; continue; }

                object v; bool ok;
                if (p.Type == typeof(double)) ok = Try<double>(data, sv.BindKey, out v);
                else if (p.Type == typeof(int)) ok = Try<int>(data, sv.BindKey, out v);
                else if (p.Type == typeof(bool)) ok = Try<bool>(data, sv.BindKey, out v);
                else ok = Try<string>(data, sv.BindKey, out v);
                if (!ok) throw new InvalidOperationException("读取绑定值失败：@" + sv.BindKey);

                string err = ScriptValueConverter.Validate(p, v);
                if (err != null) throw new InvalidOperationException(p.Name + "（@" + sv.BindKey + " = " + v + "）" + err);
                d[p.Name] = v;
            }
            return new ScriptArgs(d);
        }

        private static bool Try<T>(IDataContainer d, string key, out object v) { T x; bool ok = d.TryGet<T>(key, out x); v = x; return ok; }
    }

    /// <summary>编译加运行的一站式入口，脚本任务和需要执行脚本文本的任务类型共用。</summary>
    public static class ScriptExec
    {
        public static string Check(IScriptCommandRegistry reg, string script)
        { CompileResult r = new ScriptCompiler(reg).Compile(script); return r.Ok ? null : r.ErrorText(); }

        public static async Task RunAsync(IScriptCommandRegistry reg, string script, IRunContext ctx, CancellationToken ct)
        {
            CompileResult r = new ScriptCompiler(reg).Compile(script);
            if (!r.Ok) throw new InvalidOperationException("脚本校验失败：\n" + r.ErrorText());
            await ScriptRunner.RunAsync(r.Lines, ctx, ct);
        }
    }
}

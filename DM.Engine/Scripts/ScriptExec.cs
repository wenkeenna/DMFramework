using DM.Core.Common;
using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Scripting
{
    public static class ScriptExec
    {
        public static string Check(IScriptCommandRegistry reg, string script)
        {
            CompileResult r = new ScriptCompiler(reg).Compile(script);
            return r.Ok ? null : r.ErrorText();
        }

        public static async Task RunAsync(IScriptCommandRegistry reg, string script, IRunContext ctx, CancellationToken ct)
        {
            CompileResult r = new ScriptCompiler(reg).Compile(script);
            if (!r.Ok) throw new InvalidOperationException("脚本校验失败：\n" + r.ErrorText());
            await ScriptRunner.RunAsync(r.Lines, ctx, ct);
        }
    }
}

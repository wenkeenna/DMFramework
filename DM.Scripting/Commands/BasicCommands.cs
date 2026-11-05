using DM.Core.Common;
using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Scripting.Commands
{
    public sealed class LogCommand : IScriptCommand
    {
        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "LOG",
            Group = "通用",
            Description = "写一条日志",
            Params = new[] { new ScriptParamSpec { Name = "文本", Type = typeof(string), TakesRest = true } }
        };

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        { ctx.Log(a.Get<string>("文本")); return Task.CompletedTask; }
    }

    public sealed class DelayCommand : IScriptCommand
    {
        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "DELAY",
            Group = "通用",
            Description = "延时（毫秒）",
            Params = new[] { new ScriptParamSpec { Name = "毫秒", Type = typeof(int), Positive = true } }
        };

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct) { return Task.Delay(a.Get<int>("毫秒"), ct); }
    }
}

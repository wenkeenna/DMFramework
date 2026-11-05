using DM.Core.Common;
using DM.Scripting;
using DM.Motion.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DM.Core.Scripting;

namespace DM.Motion.Commands
{
    public sealed class WaitInCommand : IScriptCommand
    {
        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "WAIT_IN",
            Group = "IO",
            Description = "等待输入达到指定值，超时报错",
            Params = new[]
            {
                new ScriptParamSpec { Name = "输入名", Type = typeof(string) },
                new ScriptParamSpec { Name = "值", Type = typeof(bool) },
                new ScriptParamSpec { Name = "超时ms", Type = typeof(int), Required = false, Default = 5000, Description = "0 表示一直等" }
            }
        };

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        { return ctx.Get<IMotionService>().WaitInputAsync(a.Get<string>("输入名"), a.Get<bool>("值"), a.Get<int>("超时ms"), ct); }
    }
}

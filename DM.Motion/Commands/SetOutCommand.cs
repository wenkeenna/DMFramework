using DM.Core.Actions;
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
    public sealed class SetOutCommand : IScriptCommand
    {
        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "SET_OUT",
            Group = "IO",
            Description = "设置输出",
            Params = new[]
            {
                new ScriptParamSpec { Name = "输出名", Type = typeof(string) },
                new ScriptParamSpec { Name = "值", Type = typeof(bool), Description = "1/0 或 true/false" }
            }
        };

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            string name = a.Get<string>("输出名"); bool on = a.Get<bool>("值");
            var motion = ctx.Get<IMotionService>();
            return ctx.Get<IActionGateway>().RunAutoAsync("SET_OUT", name,
                () => { motion.SetOutput(name, on); return Task.CompletedTask; });
        }
    }
}

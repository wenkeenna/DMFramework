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
    public sealed class HomeCommand : IScriptCommand
    {
        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "HOME",
            Group = "轴控制",
            Description = "轴回零",
            Params = new[] { new ScriptParamSpec { Name = "轴名", Type = typeof(string) } }
        };

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            string axis = a.Get<string>("轴名");
            var motion = ctx.Get<IMotionService>();
            return ctx.Get<IActionGateway>().RunAutoAsync("HOME", axis, () => motion.HomeAsync(axis, ct));
        }
    }
}

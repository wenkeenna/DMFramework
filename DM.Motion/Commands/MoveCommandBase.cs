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
    public abstract class MoveCommandBase : IScriptCommand
    {
        private readonly bool _relative;

        protected MoveCommandBase(string name, string description, bool relative)
        {
            _relative = relative;
            Spec = new ScriptCommandSpec
            {
                Name = name,
                Group = "轴控制",
                Description = description,
                Params = new[]
                {
                    new ScriptParamSpec { Name = "轴名", Type = typeof(string) },
                    new ScriptParamSpec { Name = "位置", Type = typeof(double), AllowBinding = true },
                    new ScriptParamSpec { Name = "速度", Type = typeof(double), Required = false, Default = 0.0,
                                          Positive = true, Description = "省略则用轴默认速度" }
                }
            };
        }

        public ScriptCommandSpec Spec { get; }

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            string axis = a.Get<string>("轴名");
            double pos = a.Get<double>("位置"), vel = a.Get<double>("速度");
            var motion = ctx.Get<IMotionService>();
            return ctx.Get<IActionGateway>().RunAutoAsync(Spec.Name, axis,
                () => motion.MoveAsync(axis, pos, _relative, vel > 0 ? vel : (double?)null, ct));
        }
    }
}

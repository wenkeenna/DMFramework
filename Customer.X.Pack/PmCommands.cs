using DM.Core.Actions;
using DM.Core.Common;
using DM.Core.Data;
using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Customer.X.Pack
{
    public sealed class SetPmCommand : IScriptCommand
    {
        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "SET_PM",
            Group = "功率计",
            Description = "设置功率计参数",
            Params = new[]
            {
                new ScriptParamSpec { Name = "通道", Type = typeof(string), Options = PmChannels.All },
                new ScriptParamSpec { Name = "功率dBm", Type = typeof(double), AllowBinding = true }
            }
        };

        public Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            string ch = a.Get<string>("通道"); double dbm = a.Get<double>("功率dBm");
            return ctx.Get<IActionGateway>().RunAutoAsync("SET_PM", ch, () => ctx.Get<IPowerMeter>().SetPowerAsync(ch, dbm, ct));
        }
    }

    /// <summary>READ_PM CH1 Power.Ch1：读到的值写入数据容器（运行时作用域），后面的行用 @Power.Ch1 引用。</summary>
    public sealed class ReadPmCommand : IScriptCommand
    {
        public ScriptCommandSpec Spec { get; } = new ScriptCommandSpec
        {
            Name = "READ_PM",
            Group = "功率计",
            Description = "读取功率并写入数据容器",
            Params = new[]
            {
                new ScriptParamSpec { Name = "通道", Type = typeof(string), Options = PmChannels.All },
                new ScriptParamSpec { Name = "结果键", Type = typeof(string), IsDataKey = true, Description = "如 Power.Ch1" }
            }
        };

        public async Task ExecuteAsync(ScriptArgs a, IRunContext ctx, CancellationToken ct)
        {
            string ch = a.Get<string>("通道"), key = a.Get<string>("结果键");
            double v = await ctx.Get<IPowerMeter>().ReadPowerAsync(ch, ct);
            ctx.Get<IDataContainer>().Set(key, v, DataScope.Runtime);
            ctx.Log("READ_PM " + ch + " = " + v.ToString("F3", CultureInfo.InvariantCulture) + " dBm → " + key);
        }
    }
}

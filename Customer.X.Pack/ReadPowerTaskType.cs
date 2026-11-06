using DM.Core.Common;
using DM.Core.Data;
using DM.Core.TaskList;
using DM.Engine.TaskList;
using DM.Scripting;
using DM.Scripting.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Customer.X.Pack
{
    public sealed class ReadPowerData : BindableBase
    {
        private string _channel = "CH1", _key = "Power.Ch1";
        public string Channel 
        { 
            get { return _channel; } 
            set { SetProperty(ref _channel, value ?? ""); 
            } 
        }
        public string ResultKey 
        { 
            get { return _key; } 
            set { SetProperty(ref _key, value ?? ""); 
            } 
        }
    }

    /// <summary>
    /// 客户定制任务的完整样子：可视化参数 + 直接调服务执行 + 可选地声明"能转成脚本任务"。
    /// 工艺逻辑只在 IPowerMeter 里，任务和 READ_PM 命令是它的两个入口。
    /// </summary>
    public sealed class ReadPowerTaskType : JsonTaskType<ReadPowerData>, ITaskConvertible
    {
        public const string TypeKey = "read-power";
        public ReadPowerTaskType() : base(TypeKey, "读取功率", "功率计", "ReadPowerEditorView") { }

        public string TargetTypeKey => ScriptTaskType.TypeKey;
        public string Convert(string data)
        {
            var d = TaskJson.Deserialize<ReadPowerData>(data);
            return ScriptText.Line("READ_PM", ScriptText.Tok(d.Channel), ScriptText.Tok(d.ResultKey));
        }

        protected override string Check(ReadPowerData d)
        {
            if (System.Array.IndexOf(PmChannels.All, d.Channel) < 0) return "通道只能是：" + string.Join("/", PmChannels.All);
            string r, c;
            return DataKey.TrySplit((d.ResultKey ?? "").Trim(), out r, out c) ? null : "结果键格式应为 Root.Child。";
        }

        protected override async Task RunAsync(ReadPowerData d, IRunContext ctx, CancellationToken ct)
        {
            double v = await ctx.Get<IPowerMeter>().ReadPowerAsync(d.Channel, ct);
            ctx.Get<IDataContainer>().Set(d.ResultKey.Trim(), v, DataScope.Runtime);
            ctx.Log("功率 " + d.Channel + " = " + v.ToString("F3", CultureInfo.InvariantCulture) + " dBm → " + d.ResultKey);
        }

        protected override string Describe(ReadPowerData d) { return d.Channel + " → " + d.ResultKey; }
    }

}

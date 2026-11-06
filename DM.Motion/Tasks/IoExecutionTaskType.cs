using DM.Core.Actions;
using DM.Core.Common;
using DM.Core.TaskList;
using DM.Engine.TaskList;
using DM.Motion.Services;
using DM.Scripting.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Tasks
{
    public sealed class IoExecutionData : BindableBase
    {
        public ObservableCollection<IoStep> Steps { get; } = new ObservableCollection<IoStep>();
    }

    public sealed class IoExecutionTaskType : JsonTaskType<IoExecutionData>, ITaskConvertible
    {
        public const string TypeKey = "io-steps";
        public IoExecutionTaskType() : base(TypeKey, "IO 执行", "IO", "IoExecutionEditorView") { }

        public string TargetTypeKey => ScriptTaskType.TypeKey;

        public string Convert(string data)
        {
            var d = TaskJson.Deserialize<IoExecutionData>(data);
            if (IoStepScript.CountEnabled(d.Steps) == 0) throw new InvalidOperationException("至少需要一个启用的 IO 步骤。");
            var lines = new List<string>();
            IoStepScript.Append(lines, d.Steps, "IO 步骤");
            return string.Join("\r\n", lines);
        }

        protected override string Check(IoExecutionData d)
        {
            if (IoStepScript.CountEnabled(d.Steps) == 0) return "至少需要一个启用的 IO 步骤。";
            return IoStepRunner.Check(d.Steps, "IO 步骤");
        }

        protected override Task RunAsync(IoExecutionData d, IRunContext ctx, CancellationToken ct)
        { return IoStepRunner.RunAsync(d.Steps, ctx.Get<IMotionService>(), ctx.Get<IActionGateway>(), ct); }

        protected override string Describe(IoExecutionData d) { return IoStepScript.CountEnabled(d.Steps) + " 个 IO 步骤"; }
    }
}

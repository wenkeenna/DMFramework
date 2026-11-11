using DM.Core.TaskList;
using DM.Motion.Services;
using DM.Process.Models;
using DM.Process.Services;
using DM.Process.Tasks;
using DM.UI.TaskEditing;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Process.Editors
{
    /// <summary>工艺类编辑器的公共基类：提供轴名、IO 名、力通道下拉列表。</summary>
    public abstract class ProcessEditorViewModel<TData> : TaskEditorViewModelBase<TData> where TData : BindableBase, new()
    {
        protected ProcessEditorViewModel(ITaskTypeRegistry types, string typeKey, IMotionService motion, ForceSourceProvider force) : base(types, typeKey)
        {
            AxisNames = motion.Axes.Select(a => a.Name).ToList();
            InputNames = motion.Inputs.Select(i => i.Name).ToList();
            OutputNames = motion.Outputs.Select(o => o.Name).ToList();
            ForceChannels = force.ChannelsOrEmpty();
        }

        public IReadOnlyList<string> AxisNames { get; }
        public IReadOnlyList<string> InputNames { get; }
        public IReadOnlyList<string> OutputNames { get; }
        public IReadOnlyList<string> ForceChannels { get; }
        public Array ProbeDirections { get; } = Enum.GetValues(typeof(ProbeDirection));
        public Array RetreatModes { get; } = Enum.GetValues(typeof(RetreatMode));
    }

    public sealed class GlueEditorViewModel : ProcessEditorViewModel<GlueTaskData>
    {
        public GlueEditorViewModel(ITaskTypeRegistry t, IMotionService m, ForceSourceProvider f) : base(t, GlueTaskType.TypeKey, m, f) { }
        public Array Modes { get; } = Enum.GetValues(typeof(GlueMode));
        public Array LineDirections { get; } = Enum.GetValues(typeof(GlueLineDirection));
    }

    public sealed class TouchProbeEditorViewModel : ProcessEditorViewModel<TouchProbeData>
    {
        public TouchProbeEditorViewModel(ITaskTypeRegistry t, IMotionService m, ForceSourceProvider f) : base(t, TouchProbeTaskType.TypeKey, m, f) { }
    }
}

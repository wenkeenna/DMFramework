using DM.Motion.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Manual
{
    public sealed class ManualSlot
    {
        public int Row { get; set; }               // 0..2
        public int Col { get; set; }               // 0..2
        public string Axis { get; set; }
        public bool Positive { get; set; }
        public string Label { get; set; }
    }

    public sealed class ManualPad
    {
        public string Title { get; set; }
        public List<ManualSlot> Slots { get; set; } = new List<ManualSlot>();
        public static ManualPad Cross(string title, string hAxis, string vAxis, string hName = "X", string vName = "Y")
        {
            var p = new ManualPad { Title = title };
            if (hAxis != null) { p.Slots.Add(S(1, 0, hAxis, false, hName + "-")); p.Slots.Add(S(1, 2, hAxis, true, hName + "+")); }
            if (vAxis != null) { p.Slots.Add(S(0, 1, vAxis, false, vName + "-")); p.Slots.Add(S(2, 1, vAxis, true, vName + "+")); }
            return p;
        }
        public static ManualSlot S(int row, int col, string axis, bool positive, string label)
        { return new ManualSlot { Row = row, Col = col, Axis = axis, Positive = positive, Label = label }; }
    }

    public sealed class ManualGroup
    {
        public string Name { get; set; }
        public List<ManualPad> Pads { get; set; } = new List<ManualPad>();
        /// <summary>同步轴对：键与值互为伙伴，勾选"同步"后点动一根轴，两根一起走。</summary>
        public Dictionary<string, string> SyncPairs { get; set; } = new Dictionary<string, string>();
        public double[] StepOptions { get; set; } = { 0.0001, 0.0002, 0.0005, 0.001, 0.002, 0.005, 0.01, 0.1, 1, 2 };
    }

    public interface IManualLayout { IReadOnlyList<ManualGroup> Build(); }

    /// <summary>客户项目没有写布局类时的默认布局：一个"全部轴"分组，每根轴一个 -/+ 垫。</summary>
    public sealed class DefaultManualLayout : IManualLayout
    {
        private readonly IMotionService _motion;
        public DefaultManualLayout(IMotionService motion) { _motion = motion; }

        public IReadOnlyList<ManualGroup> Build()
        {
            var g = new ManualGroup { Name = "全部轴" };
            foreach (var a in _motion.Axes)
                g.Pads.Add(new ManualPad { Title = a.Name, Slots = { ManualPad.S(1, 0, a.Name, false, "-"), ManualPad.S(1, 2, a.Name, true, "+") } });
            return new[] { g };
        }
    }
}

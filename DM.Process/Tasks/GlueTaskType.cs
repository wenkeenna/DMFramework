using DM.Core.Common;
using DM.Core.Data;
using DM.Core.Projects;
using DM.Engine.TaskList;
using DM.Process.Models;
using DM.Process.Services;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Process.Tasks
{
    public sealed class GlueTaskData : BindableBase
    {
        private GlueMode _mode; private GlueLineDirection _dir;
        private string _sx = "", _sy = "", _ex = "", _ey = "";

        public GlueMode Mode { get { return _mode; } set { SetProperty(ref _mode, value); } }
        public GlueLineDirection Direction { get { return _dir; } set { SetProperty(ref _dir, value); } }
        public string StartX { get { return _sx; } set { SetProperty(ref _sx, value ?? ""); } }      // 点模式的点也用起点
        public string StartY { get { return _sy; } set { SetProperty(ref _sy, value ?? ""); } }
        public string EndX { get { return _ex; } set { SetProperty(ref _ex, value ?? ""); } }
        public string EndY { get { return _ey; } set { SetProperty(ref _ey, value ?? ""); } }
        public GlueSettings Settings { get; set; } = new GlueSettings();
    }

    /// <summary>点胶任务：可视化参数 + 直接调用 IGlueService（工艺逻辑里有安全收尾，不适合转成顺序脚本，所以不提供转换）。</summary>
    public sealed class GlueTaskType : JsonTaskType<GlueTaskData>
    {
        public const string TypeKey = "glue";
        public GlueTaskType() : base(TypeKey, "点胶", "点胶工艺", "GlueEditorView") { }

        protected override string Check(GlueTaskData d)
        {
            string e = d.Settings.Check();
            if (e != null) return e;
            e = CoordText.Check(d.StartX, "起点 X") ?? CoordText.Check(d.StartY, "起点 Y");
            if (e == null && d.Mode == GlueMode.Line)
                e = d.Direction == GlueLineDirection.X ? CoordText.Check(d.EndX, "终点 X") : CoordText.Check(d.EndY, "终点 Y");
            return e;
        }

        protected override async Task RunAsync(GlueTaskData d, IRunContext ctx, CancellationToken ct)
        {
            var data = ctx.Get<IDataContainer>();
            bool line = d.Mode == GlueMode.Line;
            // 全部坐标和校准值先读好：任何一个读不到，设备都不会动
            double x1 = CoordText.Resolve(d.StartX, data), y1 = CoordText.Resolve(d.StartY, data);
            double x2 = line && d.Direction == GlueLineDirection.X ? CoordText.Resolve(d.EndX, data) : 0;
            double y2 = line && d.Direction == GlueLineDirection.Y ? CoordText.Resolve(d.EndY, data) : 0;
            GlueJob job = GlueJobs.Make(d.Settings, d.Mode, d.Direction, x1, y1, x2, y2, GlueJobs.CalibZ(d.Settings, data));
            await ctx.Get<IGlueService>().RunAsync(job, ctx.Log, ct);
        }

        protected override string Describe(GlueTaskData d)
        {
            return d.Mode == GlueMode.Point
                ? "点胶 (" + d.StartX + ", " + d.StartY + ")  " + d.Settings.GlueTimeMs + "ms"
                : "划线 (" + d.StartX + ", " + d.StartY + ") → " + d.Direction + " 方向" + (d.Settings.Touch.IsEnabled ? "  含探测" : "");
        }
    }

    /// <summary>点胶的默认参数：新项目自动补齐校准键，避免第一次运行报"读取数值失败"。</summary>
    public sealed class GlueDefaultsSeeder : IParameterSeeder
    {
        public void Seed(IDataContainer d)
        {
            foreach (string k in new[] { "点胶校准.BaseZ", "点胶校准.NewZ" })
                if (!d.Contains(k)) d.Set(k, 0.0, DataScope.Project);
        }
    }
}

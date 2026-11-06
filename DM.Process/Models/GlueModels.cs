using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Process.Models
{
    public enum GlueMode { Point, Line }
    public enum GlueLineDirection { X, Y }

    /// <summary>点胶前的接触探测（沿 Z 轴，力通道固定为 GlueZ）。</summary>
    public sealed class GlueTouchSettings : BindableBase
    {
        private bool _on; private ProbeDirection _dir; private RetreatMode _mode;
        private double _max = 5, _step = 0.05, _vel = 0.1, _force = 0.5, _retreat = 0.5, _retreatVel = 2;
        private int _settle = 200;

        public bool IsEnabled { get { return _on; } set { SetProperty(ref _on, value); } }
        public ProbeDirection Direction { get { return _dir; } set { SetProperty(ref _dir, value); } }
        public double MaxTravel { get { return _max; } set { SetProperty(ref _max, value); } }
        public double StepDistance { get { return _step; } set { SetProperty(ref _step, value); } }
        public double StepVelocity { get { return _vel; } set { SetProperty(ref _vel, value); } }
        public int SettleMs { get { return _settle; } set { SetProperty(ref _settle, value); } }
        public double TriggerForce { get { return _force; } set { SetProperty(ref _force, value); } }
        public RetreatMode Retreat { get { return _mode; } set { SetProperty(ref _mode, value); } }
        public double RetreatValue { get { return _retreat; } set { SetProperty(ref _retreat, value); } }
        public double RetreatVelocity { get { return _retreatVel; } set { SetProperty(ref _retreatVel, value); } }

        public ProbeParams ToProbe(string zAxis)
        {
            return new ProbeParams
            {
                Axis = (zAxis ?? "").Trim(),
                Channel = "GlueZ",
                Direction = Direction,
                MaxTravel = MaxTravel,
                StepDistance = StepDistance,
                StepVelocity = StepVelocity,
                SettleMs = SettleMs,
                TriggerForce = TriggerForce,
                Retreat = Retreat,
                RetreatValue = RetreatValue,
                RetreatVelocity = RetreatVelocity
            };
        }
    }

    /// <summary>
    /// 点胶工艺设置。点胶任务里每个任务一份；脚本命令 GLUE_POINT / GLUE_LINE 读 machine.json 的 "Glue" 段。
    /// </summary>
    public sealed class GlueSettings : BindableBase
    {
        private string _ax = "Gx", _ay = "Gy", _az = "Glue", _cyl = "点胶气缸", _glue = "点胶机", _fwd = "点胶下限", _back = "点胶上限",
                       _cRoot = "点胶校准", _cBase = "BaseZ", _cNew = "NewZ";
        private double _move = 10, _line = 1, _z, _safe = 1;
        private int _ms = 100, _cto = 2000;
        private bool _keep, _calib = true;

        public string AxisX { get { return _ax; } set { SetProperty(ref _ax, value ?? ""); } }
        public string AxisY { get { return _ay; } set { SetProperty(ref _ay, value ?? ""); } }
        public string AxisZ { get { return _az; } set { SetProperty(ref _az, value ?? ""); } }
        public double MoveSpeed { get { return _move; } set { SetProperty(ref _move, value); } }
        public double LineSpeed { get { return _line; } set { SetProperty(ref _line, value); } }
        public int GlueTimeMs { get { return _ms; } set { SetProperty(ref _ms, value); } }
        public double ZPos { get { return _z; } set { SetProperty(ref _z, value); } }
        public double ZSafeOffset { get { return _safe; } set { SetProperty(ref _safe, value); } }
        public bool KeepCylinderExtended { get { return _keep; } set { SetProperty(ref _keep, value); } }
        public string CylinderOutput { get { return _cyl; } set { SetProperty(ref _cyl, value ?? ""); } }
        public string GlueOutput { get { return _glue; } set { SetProperty(ref _glue, value ?? ""); } }
        public string CylinderForwardInput { get { return _fwd; } set { SetProperty(ref _fwd, value ?? ""); } }
        public string CylinderBackInput { get { return _back; } set { SetProperty(ref _back, value ?? ""); } }
        public int CylinderTimeoutMs { get { return _cto; } set { SetProperty(ref _cto, value); } }
        public bool CalibrationEnabled { get { return _calib; } set { SetProperty(ref _calib, value); } }
        public string CalibRoot { get { return _cRoot; } set { SetProperty(ref _cRoot, value ?? ""); } }
        public string CalibBaseKey { get { return _cBase; } set { SetProperty(ref _cBase, value ?? ""); } }
        public string CalibNewKey { get { return _cNew; } set { SetProperty(ref _cNew, value ?? ""); } }
        public GlueTouchSettings Touch { get; set; } = new GlueTouchSettings();

        public string CalibKey(string child) { return CalibRoot.Trim() + "." + child.Trim(); }

        public string Check()
        {
            string[] axes = { AxisX, AxisY, AxisZ };
            if (axes.Any(string.IsNullOrWhiteSpace)) return "X/Y/Z 轴名不能为空。";
            if (axes.Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3) return "X/Y/Z 必须是三根不同的轴。";
            if (MoveSpeed <= 0 || LineSpeed <= 0) return "速度必须大于 0。";
            if (GlueTimeMs < 0) return "点胶时间不能为负数。";
            if (ZSafeOffset <= 0) return "Z 安全偏移必须大于 0。";
            if (CylinderTimeoutMs <= 0) return "气缸超时必须大于 0。";
            if (new[] { CylinderOutput, GlueOutput, CylinderForwardInput, CylinderBackInput }.Any(string.IsNullOrWhiteSpace)) return "点胶 IO 名称不能为空。";
            if (CalibrationEnabled && new[] { CalibRoot, CalibBaseKey, CalibNewKey }.Any(string.IsNullOrWhiteSpace)) return "校准键不能为空。";
            return Touch.IsEnabled ? Touch.ToProbe(AxisZ).Check() : null;
        }
    }

    /// <summary>已解析完所有数值的一次点胶作业，服务只认数字，不碰数据容器。</summary>
    public sealed class GlueJob
    {
        public GlueSettings Settings;
        public GlueMode Mode;
        public GlueLineDirection Direction;
        public double X1, Y1, X2, Y2, CalibZ;
    }
}

using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Process.Models
{
    public enum ProbeDirection { Positive, Negative }
    public enum RetreatMode { Relative, Absolute }

    /// <summary>一次探测的全部参数（已是纯数据，服务只认它）。</summary>
    public sealed class ProbeParams
    {
        public string Axis, Channel;
        public ProbeDirection Direction;
        public double MaxTravel = 5, StepDistance = 0.05, StepVelocity = 0.1, TriggerForce = 0.5;
        public int SettleMs = 200;
        public RetreatMode Retreat;
        public double RetreatValue = 0.5, RetreatVelocity = 2;

        public string Check()
        {
            if (string.IsNullOrWhiteSpace(Axis)) return "轴不能为空。";
            if (string.IsNullOrWhiteSpace(Channel)) return "力通道不能为空。";
            if (MaxTravel <= 0) return "最大行程必须大于 0。";
            if (StepDistance <= 0) return "步距必须大于 0。";
            if (StepDistance > MaxTravel) return "步距不能大于最大行程。";
            if (StepVelocity <= 0) return "速度必须大于 0。";
            if (TriggerForce <= 0) return "触发力必须大于 0。";
            if (SettleMs < 0) return "稳定时间不能为负数。";
            if (RetreatValue < 0 && Retreat == RetreatMode.Relative) return "回退距离不能为负数。";
            if (RetreatVelocity <= 0) return "回退速度必须大于 0。";
            return null;
        }
    }

    /// <summary>接触探测任务的一个通道（左 / 右）。</summary>
    public sealed class ProbeLaneData : BindableBase
    {
        private bool _enabled; private string _name = "", _axis = "", _channel = "";
        private ProbeDirection _dir;
        private double _max = 5, _step = 0.05, _vel = 0.1, _force = 0.5, _retreat = 0.5, _retreatVel = 2;
        private int _settle = 200;

        public bool Enabled { get { return _enabled; } set { SetProperty(ref _enabled, value); } }
        public string Name { get { return _name; } set { SetProperty(ref _name, value ?? ""); } }
        public string Axis { get { return _axis; } set { SetProperty(ref _axis, value ?? ""); } }
        public string Channel { get { return _channel; } set { SetProperty(ref _channel, value ?? ""); } }
        public ProbeDirection Direction { get { return _dir; } set { SetProperty(ref _dir, value); } }
        public double MaxTravel { get { return _max; } set { SetProperty(ref _max, value); } }
        public double StepDistance { get { return _step; } set { SetProperty(ref _step, value); } }
        public double StepVelocity { get { return _vel; } set { SetProperty(ref _vel, value); } }
        public int SettleMs { get { return _settle; } set { SetProperty(ref _settle, value); } }
        public double TriggerForce { get { return _force; } set { SetProperty(ref _force, value); } }
        public double RetreatValue { get { return _retreat; } set { SetProperty(ref _retreat, value); } }
        public double RetreatVelocity { get { return _retreatVel; } set { SetProperty(ref _retreatVel, value); } }

        public ProbeParams ToProbe()
        {
            return new ProbeParams
            {
                Axis = Axis.Trim(),
                Channel = Channel.Trim(),
                Direction = Direction,
                MaxTravel = MaxTravel,
                StepDistance = StepDistance,
                StepVelocity = StepVelocity,
                SettleMs = SettleMs,
                TriggerForce = TriggerForce,
                Retreat = RetreatMode.Relative,
                RetreatValue = RetreatValue,
                RetreatVelocity = RetreatVelocity
            };
        }
    }

    public sealed class TouchProbeData : BindableBase
    {
        private bool _parallel = true;
        public bool RunInParallel { get { return _parallel; } set { SetProperty(ref _parallel, value); } }
        public ProbeLaneData Left { get; set; } = new ProbeLaneData { Name = "左通道" };
        public ProbeLaneData Right { get; set; } = new ProbeLaneData { Name = "右通道" };
    }
}

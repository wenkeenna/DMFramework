using DM.Core.Events;
using DM.Core.TaskList;
using DM.Motion.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DM.Motion.Manual.ViewModels
{
    public sealed class SlotVm
    {
        public SlotVm(ManualSlot s) { Row = s.Row; Col = s.Col; Axis = s.Axis; Positive = s.Positive; Label = s.Label; }
        public int Row { get; }
        public int Col { get; }
        public string Axis { get; }
        public bool Positive { get; }
        public string Label { get; }
    }

    public sealed class PadVm { public string Title { get; set; } public List<SlotVm> Slots { get; } = new List<SlotVm>(); }

    public sealed class AxisRow : BindableBase
    {
        private double _pos, _step = 0.001; private bool _moving, _alarm, _homed;
        public AxisRow(string name, double[] stepOptions) { Name = name; StepOptions = stepOptions; }
        public string Name { get; }
        public double[] StepOptions { get; }
        public double Position { get { return _pos; } set { SetProperty(ref _pos, value); } }
        public double Step { get { return _step; } set { SetProperty(ref _step, value); } }
        public bool Moving { get { return _moving; } set { SetProperty(ref _moving, value); } }
        public bool Alarm { get { return _alarm; } set { SetProperty(ref _alarm, value); } }
        public bool Homed { get { return _homed; } set { SetProperty(ref _homed, value); } }
    }

    public sealed class GroupVm : BindableBase
    {
        private bool _sync;
        public string Name { get; set; }
        public List<PadVm> Pads { get; } = new List<PadVm>();
        public List<AxisRow> Axes { get; } = new List<AxisRow>();
        public Dictionary<string, string> SyncPairs { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool HasSync => SyncPairs.Count > 0;
        public bool SyncEnabled { get { return _sync; } set { SetProperty(ref _sync, value); } }
    }

    public sealed class IoRow : BindableBase
    {
        private bool _v;
        public IoRow(string name) { Name = name; }
        public string Name { get; }
        public bool Value { get { return _v; } set { SetProperty(ref _v, value); } }
    }

    /// <summary>
    /// 点动：模式"点动"= 按下走一步（步距按轴选）；模式"连续"= 按住连续走、松开停；按住 Ctrl 点按钮临时按连续。
    /// 面板显示时每 100ms 读一次轴状态，未连接时不读。运行任务时不允许手动运动。
    /// </summary>
    public sealed class ManualMotionViewModel : BindableBase
    {
        private readonly ManualMotionService _manual;
        private readonly IMotionService _motion;
        private readonly ITaskRunner _runner;
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        private GroupVm _active;
        private SlotVm _pressed;
        private string _jogAxis, _status = "";
        private double _speed = 1;
        private bool _continuous;
        private int _tick;

        public ManualMotionViewModel(ManualMotionService manual, IMotionService motion, IManualLayout layout, ITaskRunner runner, IEventAggregator events)
        {
            _manual = manual; _motion = motion; _runner = runner;
            Build(layout);

            _timer.Tick += (s, e) => Refresh();
            events.GetEvent<EmergencyStopEvent>().Subscribe(m => { if (m != null && m.IsActive) { _pressed = null; _jogAxis = null; Status = "急停，已停止手动运动。"; } }, ThreadOption.UIThread, true);

            SetSpeedCommand = new DelegateCommand<string>(s => Speed = double.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            StopAllCommand = new DelegateCommand(() => { _manual.StopAll(); Status = "已停止所有轴。"; });
            HomeCommand = new DelegateCommand<AxisRow>(async r => await Run("回零 " + r.Name, () => _manual.HomeAsync(r.Name, CancellationToken.None)));
            ClearAlarmCommand = new DelegateCommand<AxisRow>(async r =>
            {
                if (MessageBox.Show("清报警会先关闭伺服使能，竖直轴可能下落。\n确定对 " + r.Name + " 清报警吗？", "清报警", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
                await Run("清报警 " + r.Name, () => _manual.ClearAlarmAsync(r.Name, CancellationToken.None));
            });
            ToggleOutputCommand = new DelegateCommand<IoRow>(async r => await Run("输出 " + r.Name, () => _manual.SetOutputAsync(r.Name, !r.Value)));
        }

        public ObservableCollection<GroupVm> Groups { get; } = new ObservableCollection<GroupVm>();
        public ObservableCollection<IoRow> Inputs { get; } = new ObservableCollection<IoRow>();
        public ObservableCollection<IoRow> Outputs { get; } = new ObservableCollection<IoRow>();
        public GroupVm ActiveGroup { get { return _active; } set { SetProperty(ref _active, value); } }
        public double Speed { get { return _speed; } set { SetProperty(ref _speed, value); } }
        public bool Continuous { get { return _continuous; } set { SetProperty(ref _continuous, value); } }
        public string Status { get { return _status; } private set { SetProperty(ref _status, value); } }

        public DelegateCommand<string> SetSpeedCommand { get; }
        public DelegateCommand StopAllCommand { get; }
        public DelegateCommand<AxisRow> HomeCommand { get; }
        public DelegateCommand<AxisRow> ClearAlarmCommand { get; }
        public DelegateCommand<IoRow> ToggleOutputCommand { get; }

        public void Start() { _timer.Start(); }
        public void Stop() { _timer.Stop(); ReleaseJog(); }          // 视图卸载时一定停掉连续点动

        private void Build(IManualLayout layout)
        {
            var known = new HashSet<string>(_motion.Axes.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
            var skipped = new List<string>();
            foreach (ManualGroup g in layout.Build())
            {
                var gv = new GroupVm { Name = g.Name };
                foreach (var kv in g.SyncPairs)
                    if (known.Contains(kv.Key) && known.Contains(kv.Value)) { gv.SyncPairs[kv.Key] = kv.Value; gv.SyncPairs[kv.Value] = kv.Key; }

                foreach (ManualPad p in g.Pads)
                {
                    var pv = new PadVm { Title = p.Title };
                    foreach (ManualSlot s in p.Slots)
                    {
                        if (!known.Contains(s.Axis ?? "")) { skipped.Add(s.Axis + "(" + s.Label + ")"); continue; }
                        pv.Slots.Add(new SlotVm(s));
                        if (!gv.Axes.Any(a => string.Equals(a.Name, s.Axis, StringComparison.OrdinalIgnoreCase))) gv.Axes.Add(new AxisRow(s.Axis, g.StepOptions));
                    }
                    if (pv.Slots.Count > 0) gv.Pads.Add(pv);
                }
                if (gv.Pads.Count > 0) Groups.Add(gv);
            }
            ActiveGroup = Groups.FirstOrDefault();
            foreach (var i in _motion.Inputs) Inputs.Add(new IoRow(i.Name));
            foreach (var o in _motion.Outputs) Outputs.Add(new IoRow(o.Name));
            if (skipped.Count > 0) Status = "布局里这些轴没有在运动配置中定义，已跳过：" + string.Join("、", skipped);
        }

        // ───── 点动按钮（由视图的鼠标事件调用）─────
        public async void Down(SlotVm slot, bool ctrl)
        {
            if (slot == null || ActiveGroup == null) return;
            if (_runner.IsRunning) { Status = "任务运行中，不能手动运动。"; return; }
            _pressed = slot;
            bool continuous = Continuous || ctrl;
            var row = ActiveGroup.Axes.First(a => string.Equals(a.Name, slot.Axis, StringComparison.OrdinalIgnoreCase));
            string partner;
            bool sync = ActiveGroup.SyncEnabled && ActiveGroup.SyncPairs.TryGetValue(slot.Axis, out partner);
            try
            {
                if (continuous)
                {
                    if (sync) { Status = "同步模式只支持点动。"; return; }
                    _jogAxis = slot.Axis;
                    await _manual.JogStartAsync(slot.Axis, slot.Positive, Speed);
                }
                else if (sync) await _manual.StepPairAsync(slot.Axis, ActiveGroup.SyncPairs[slot.Axis], slot.Positive, row.Step, Speed, CancellationToken.None);
                else await _manual.StepAsync(slot.Axis, slot.Positive, row.Step, Speed, CancellationToken.None);
            }
            catch (Exception ex) { _jogAxis = null; Status = slot.Label + " 失败：" + ex.Message; }
        }

        public void Up(SlotVm slot)
        {
            if (slot == null || _pressed != slot) return;
            _pressed = null;
            ReleaseJog();
        }

        private void ReleaseJog()
        {
            string axis = _jogAxis; _jogAxis = null;
            if (axis != null) { try { _manual.JogStop(axis); } catch (Exception ex) { Status = "停止失败：" + ex.Message; } }
        }

        private async Task Run(string what, Func<Task> action)
        {
            try { await action(); Status = what + " 完成。"; }
            catch (Exception ex) { Status = what + " 失败：" + ex.Message; }
        }

        // ───── 刷新 ─────
        private void Refresh()
        {
            if (!_motion.IsConnected) return;
            try
            {
                foreach (GroupVm g in Groups)
                    foreach (AxisRow r in g.Axes)
                    {
                        var s = _motion.GetStatus(r.Name);
                        r.Position = s.Position; r.Moving = s.IsMoving; r.Alarm = s.Alarm; r.Homed = s.Homed;
                    }
                if (++_tick % 3 != 0) return;                       // IO 每 300ms 读一次
                foreach (IoRow i in Inputs) i.Value = _motion.GetInput(i.Name);
                foreach (IoRow o in Outputs) o.Value = _motion.GetOutput(o.Name);
            }
            catch (Exception ex) { Status = "读取状态失败：" + ex.Message; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Models
{
    /// <summary>
    /// 一个项目的运动配置。客户项目写一个类实现它（通常继承 MotionProfileBase）并注册进容器。
    /// 注册了它，machine.json 里的 Motion 段就不再读取。
    /// </summary>
    public interface IMotionProfile
    {
        /// <summary>配置名称，用于日志和错误提示，如 "X客户-点胶机 V2"。</summary>
        string Name { get; }
        MotionConfig Build();
    }

    /// <summary>
    /// 流式写法的基类。子类在 Define 里调用 Axis / Input / Output，Build 时统一产生 MotionConfig。
    /// 单位约定：位置、速度的单位与 PulsePerUnit 一致（例如毫米、毫米/秒）。
    /// </summary>
    public abstract class MotionProfileBase : IMotionProfile
    {
        private readonly List<AxisConfig> _axes = new List<AxisConfig>();
        private readonly List<IoConfig> _in = new List<IoConfig>();
        private readonly List<IoConfig> _out = new List<IoConfig>();
        private string _driver;

        public abstract string Name { get; }

        /// <summary>在这里写轴和 IO 的定义。</summary>
        protected abstract void Define();

        protected void Driver(string name) { _driver = name; }

        /// <summary>定义一根轴。后续用 lambda 补充可选参数。</summary>
        protected AxisBuilder Axis(string name, int card, int index, double pulsePerUnit, double maxVelocity)
        {
            var a = new AxisConfig
            {
                Name = name,
                Card = card,
                Index = index,
                PulsePerUnit = pulsePerUnit,
                MaxVelocity = maxVelocity,
                DefaultVelocity = Math.Min(50, maxVelocity)
            };
            _axes.Add(a);
            return new AxisBuilder(a);
        }

        /// <summary>输入点。inverted=true 表示低电平有效（物理导通读到 0）。</summary>
        protected void Input(string name, int card, int bit, bool inverted = false)
        { _in.Add(new IoConfig { Name = name, Card = card, Bit = bit, Inverted = inverted }); }

        protected void Output(string name, int card, int bit, bool inverted = false)
        { _out.Add(new IoConfig { Name = name, Card = card, Bit = bit, Inverted = inverted }); }

        public MotionConfig Build()
        {
            _axes.Clear(); _in.Clear(); _out.Clear(); _driver = null;
            Define();                                           // 每次 Build 都重新定义，结果不受上次影响
            return new MotionConfig
            {
                Driver = _driver,
                Axes = new List<AxisConfig>(_axes),
                Inputs = new List<IoConfig>(_in),
                Outputs = new List<IoConfig>(_out)
            };
        }

    }


    /// <summary>轴的可选参数，链式书写。</summary>
    public sealed class AxisBuilder
    {
        private readonly AxisConfig _a;
        public AxisBuilder(AxisConfig a) { _a = a; }

        public AxisBuilder DefaultVelocity(double v) { _a.DefaultVelocity = v; return this; }
        public AxisBuilder AccDec(double accSec, double decSec) { _a.AccTime = accSec; _a.DecTime = decSec; return this; }
        public AxisBuilder SoftLimit(double min, double max) { _a.MinPos = min; _a.MaxPos = max; return this; }
        public AxisBuilder InPosTolerance(double v) { _a.InPosTolerance = v; return this; }
        public AxisBuilder Home(double velocity, double offset = 0, int mode = 0)
        { _a.HomeVelocity = velocity; _a.HomeOffset = offset; _a.HomeMode = mode; return this; }
    }
    /// <summary>延迟获取 IMotionProfile：没有注册时返回 null，不报错。</summary>
    public sealed class MotionProfileProvider
    {
        private readonly Func<IMotionProfile> _get;
        public MotionProfileProvider(Func<IMotionProfile> get) { _get = get; }

        public IMotionProfile Get()
        {
            try { return _get(); }
            catch (Exception) { return null; }       // 没有注册配置类
        }
    }
}

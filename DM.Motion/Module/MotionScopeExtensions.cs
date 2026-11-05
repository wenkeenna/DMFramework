using DM.Engine.Hosting;
using DM.Motion.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Module
{
    public static class MotionScopeExtensions
    {
        /// <summary>驱动模块用它登记自己：scope.MotionDriver("Simulator", () => new SimulatorDriver())</summary>
        public static ModuleScope MotionDriver(this ModuleScope s, string name, Func<IMotionDriver> factory)
        { s.Resolve<IMotionDriverCatalog>().Add(name, factory); return s; }
    }
}

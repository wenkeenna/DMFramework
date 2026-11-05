using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Models
{
    public sealed class IoConfig
    {
        public string Name { get; set; }
        public int Card { get; set; }
        public int Bit { get; set; }
        public bool Inverted { get; set; }                    // 低电平有效
    }
}

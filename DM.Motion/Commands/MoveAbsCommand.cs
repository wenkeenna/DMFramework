using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Commands
{
    public sealed class MoveAbsCommand : MoveCommandBase 
    { 
        public MoveAbsCommand() : base("MOVE_ABS", "绝对运动", false) { } 
    }
}

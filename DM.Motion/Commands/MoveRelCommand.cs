using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Motion.Commands
{
    public sealed class MoveRelCommand : MoveCommandBase 
    { 
        public MoveRelCommand() : base("MOVE_REL", "相对运动", true) { } 
    }
}

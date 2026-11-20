using DM.Motion.Manual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Customer.X.Pack.Motion
{
    public sealed class XManualLayout : IManualLayout
    {
        public IReadOnlyList<ManualGroup> Build()
        {
            return new[]
            {
            new ManualGroup { Name = "龙门组", Pads =
                { ManualPad.Cross("XY", Axes.Gx, Axes.Gy),
                  new ManualPad { Title = "Cam/Glue", Slots = {
                      ManualPad.S(0, 0, "Cam", false, "Cam-"), ManualPad.S(2, 0, "Cam", true, "Cam+"),
                      ManualPad.S(0, 1, "Glue", false, "Glue-"), ManualPad.S(2, 1, "Glue", true, "Glue+") } } } },
            new ManualGroup { Name = "左FA",
                SyncPairs = { { "L_X", "R_X" }, { "L_Y", "R_Y" }, { "L_Z", "R_Z" } },
                Pads = { ManualPad.Cross("Left", "L_Z", "L_X", "Z", "X"),
                         new ManualPad { Title = "Y", Slots = { ManualPad.S(0, 0, "L_Y", false, "Y-"), ManualPad.S(2, 0, "L_Y", true, "Y+") } },
                         new ManualPad { Title = "Rxyz", Slots = {
                             ManualPad.S(1, 0, "L_Rx", false, "Rx-"), ManualPad.S(1, 2, "L_Rx", true, "Rx+"),
                             ManualPad.S(0, 1, "L_Ry", false, "Ry-"), ManualPad.S(2, 1, "L_Ry", true, "Ry+"),
                             ManualPad.S(0, 0, "L_Rz", false, "Rz-"), ManualPad.S(0, 2, "L_Rz", true, "Rz+") } } } }
            // 右FA 同理
        };
        }
    }
}

using DM.Core.TaskList;
using DM.UI.TaskEditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Customer.X.Pack
{
    public sealed class ReadPowerEditorViewModel : TaskEditorViewModelBase<ReadPowerData>
    {
        public ReadPowerEditorViewModel(ITaskTypeRegistry types) : base(types, ReadPowerTaskType.TypeKey) { }
        public string[] Channels => PmChannels.All;
    }
}

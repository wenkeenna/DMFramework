using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Shell
{
    public sealed class ShellOptions
    {
        public ShellOptions(string title) { Title = title; }
        public string Title { get; }
    }
}

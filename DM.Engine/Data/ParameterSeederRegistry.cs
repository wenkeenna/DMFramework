using DM.Core.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.Data
{
    public sealed class ParameterSeederRegistry : IParameterSeederRegistry
    {
        private readonly List<IParameterSeeder> _l = new List<IParameterSeeder>();
        public void Register(IParameterSeeder s) { lock (_l) { if (!_l.Contains(s)) _l.Add(s); } }
        public IReadOnlyList<IParameterSeeder> All { get { lock (_l) return _l.ToList(); } }
    }
}

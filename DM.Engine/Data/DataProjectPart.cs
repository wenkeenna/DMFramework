using DM.Core.Common;
using DM.Core.Data;
using DM.Core.Projects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Engine.Data
{
    /// <summary>Data 阶段参与者：打开时载入并播种默认值，保存/关闭时落盘。载入失败时禁止保存，防止空数据覆盖可修复的文件。</summary>
    public sealed class DataProjectPart : IProjectAware, IProjectSavable
    {
        private readonly DataContainer _data;
        private readonly AppPaths _paths;
        private readonly IParameterSeederRegistry _seeders;
        private bool _loaded;

        public DataProjectPart(DataContainer data, AppPaths paths, IParameterSeederRegistry seeders)
        { _data = data; _paths = paths; _seeders = seeders; }

        public string Name => "Data";
        public ProjectPhase Phase => ProjectPhase.Data;

        public Task OnOpenedAsync(ProjectContext ctx, CancellationToken ct)
        {
            _loaded = false;
            string file = Path.Combine(ctx.Directory, "params.json");
            _data.ClearScope(DataScope.Runtime);
            _data.LoadFile(MachineFile, DataScope.Machine);
            _data.LoadFile(file, DataScope.Project);

            using (_data.BeginLoad())                       // 播种不产生审计噪音
                foreach (IParameterSeeder s in _seeders.All) s.Seed(_data);

            _loaded = true;
            if (!File.Exists(file)) _data.SaveFile(file, DataScope.Project);    // 新项目：文件夹立即完整
            return Task.CompletedTask;
        }

        public Task OnSaveAsync(ProjectContext ctx, CancellationToken ct) { Save(ctx); return Task.CompletedTask; }

        public Task OnClosingAsync(ProjectContext ctx, CancellationToken ct)
        {
            try { if (_loaded) Save(ctx); }
            finally
            {
                _data.ClearScope(DataScope.Project);
                _data.ClearScope(DataScope.Runtime);
                _loaded = false;
            }
            return Task.CompletedTask;
        }

        private void Save(ProjectContext ctx)
        {
            if (!_loaded) throw new InvalidOperationException("参数文件载入失败，为避免覆盖原文件已禁止保存。");
            _data.SaveFile(Path.Combine(ctx.Directory, "params.json"), DataScope.Project);
            _data.SaveFile(MachineFile, DataScope.Machine);
        }

        private string MachineFile => Path.Combine(_paths.Config, "machine-data.json");
    }
}

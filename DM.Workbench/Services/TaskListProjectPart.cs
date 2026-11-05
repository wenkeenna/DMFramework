using DM.Core.Projects;
using DM.Core.TaskList;
using DM.Engine.TaskList;
using DM.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Workbench.Services
{
    /// <summary>
    /// 打开项目时读 tasks.json，关闭时脏则保存，"保存"按钮走 OnSaveAsync。
    /// 读取失败（文件损坏）时列表保持为空，且之后一切保存都被拒绝，不会用空列表覆盖原文件。
    /// 协调器在线程池里调用这里，所以所有对 editor 的访问都先切回 UI 线程。
    /// </summary>
    public sealed class TaskListProjectPart : IProjectAware, IProjectSavable
    {
        private readonly TaskListEditor _editor;
        private bool _loaded;

        public TaskListProjectPart(TaskListEditor editor) { _editor = editor; }

        public string Name => "TaskList";
        public ProjectPhase Phase => ProjectPhase.Service;

        public Task OnOpenedAsync(ProjectContext ctx, CancellationToken ct)
        {
            _loaded = false;
            UiThread.Invoke(_editor.Clear);
            List<TaskItem> items = TaskFile.Load(Path.Combine(ctx.Directory, TaskFile.FileName));    // 损坏则抛异常，上报为本模块失败
            UiThread.Invoke(() => _editor.Load(items));
            _loaded = true;
            return Task.CompletedTask;
        }

        public Task OnSaveAsync(ProjectContext ctx, CancellationToken ct) { Save(ctx); return Task.CompletedTask; }

        public Task OnClosingAsync(ProjectContext ctx, CancellationToken ct)
        {
            try
            {
                bool dirty = false;
                UiThread.Invoke(() => dirty = _editor.IsDirty);
                if (_loaded && dirty) Save(ctx);                 // 关闭时自动保存；编辑已全部留痕
            }
            finally
            {
                _loaded = false;
                UiThread.Invoke(_editor.Clear);                  // 保存失败也要清空，否则旧项目的任务会留在新项目的界面上
            }
            return Task.CompletedTask;
        }

        private void Save(ProjectContext ctx)
        {
            if (!_loaded)
                throw new InvalidOperationException("任务文件载入失败，为避免覆盖原文件已禁止保存。请修复或删除 tasks.json 后重新打开项目。");

            IReadOnlyList<TaskItem> items = null;
            UiThread.Invoke(() => items = _editor.ToItems());
            TaskFile.Save(Path.Combine(ctx.Directory, TaskFile.FileName), items);
            UiThread.Invoke(_editor.MarkSaved);
        }
    }
}

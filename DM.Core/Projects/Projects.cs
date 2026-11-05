using DM.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Core.Projects
{
    /// <summary>粗粒度阶段代替数字优先级：模块只回答"我属于哪一类"，不用关心排第几。</summary>
    public enum ProjectPhase
    {
        Data = 0,       // 载入参数，必须最先完成；失败会中止项目打开
        Hardware = 1,   // 从数据容器取配置，初始化硬件相关对象
        Service = 2,    // 依赖硬件的业务服务（任务列表等）
        Ui = 3          // 界面刷新、布局恢复
    }

    public sealed class ProjectContext
    {
        public ProjectContext(string name, string directory, IDataContainer data)
        { Name = name; Directory = directory; Data = data; }
        public string Name { get; }
        public string Directory { get; }
        public IDataContainer Data { get; }
    }

    /// <summary>
    /// 随项目加载、卸载而初始化、释放的模块实现它。
    /// OnOpenedAsync 抛异常只代表本模块失败（Data 阶段除外，会中止整个打开）。
    /// OnClosingAsync 只对"打开成功"的参与者调用，异常被记录并忽略。
    /// 协调器全程 ConfigureAwait(false)：参与者要改界面对象，必须自己切回 UI 线程。
    /// </summary>
    public interface IProjectAware
    {
        string Name { get; }
        ProjectPhase Phase { get; }
        Task OnOpenedAsync(ProjectContext context, CancellationToken ct);
        Task OnClosingAsync(ProjectContext context, CancellationToken ct);
    }

    /// <summary>可选：用户点"保存"时被调用。</summary>
    public interface IProjectSavable { Task OnSaveAsync(ProjectContext context, CancellationToken ct); }

    public interface IProjectAwareRegistry { void Register(IProjectAware participant); }

    public interface IProjectLifecycle
    {
        ProjectContext Current { get; }
        Task<ProjectOpenResult> OpenAsync(string name, string directory, CancellationToken ct = default(CancellationToken));
        Task<IReadOnlyList<ProjectAwareFailure>> SaveAsync(CancellationToken ct = default(CancellationToken));
        Task CloseAsync(CancellationToken ct = default(CancellationToken));
    }

    public sealed class ProjectAwareFailure
    {
        public ProjectAwareFailure(string name, ProjectPhase phase, Exception error)
        { Name = name; Phase = phase; Error = error; }
        public string Name { get; }
        public ProjectPhase Phase { get; }
        public Exception Error { get; }
    }

    public sealed class ProjectOpenResult
    {
        public ProjectOpenResult(bool aborted, IReadOnlyList<ProjectAwareFailure> failures)
        { Aborted = aborted; Failures = failures; }
        public bool Aborted { get; }                // Data 阶段失败，项目未打开
        public bool Success => !Aborted;
        public IReadOnlyList<ProjectAwareFailure> Failures { get; }
        public bool HasWarnings => !Aborted && Failures.Count > 0;
    }

    public sealed class ProjectInfo
    {
        public string Name { get; set; }
        public string Directory { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public interface IProjectCatalog
    {
        IReadOnlyList<ProjectInfo> List();
        ProjectInfo Find(string name);                          // 不存在或名称非法返回 null
        ProjectInfo Create(string name, out string error);      // 失败返回 null 并给出原因
    }

    /// <summary>Read 在记录缺失、损坏、目录已不存在时返回 null。</summary>
    public interface ILastProjectStore { ProjectInfo Read(); void Write(string name); void Clear(); }

    /// <summary>默认参数播种：只补缺失的键，必须幂等（新项目和打开旧项目都会调用）。</summary>
    public interface IParameterSeeder { void Seed(IDataContainer data); }
    public interface IParameterSeederRegistry { void Register(IParameterSeeder seeder); IReadOnlyList<IParameterSeeder> All { get; } }
}

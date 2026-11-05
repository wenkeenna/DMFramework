using DM.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Core.TaskList
{
    /// <summary>任务的持久化模型，只有数据。运行状态（运行中/成功/耗时）属于界面行对象，不放这里。</summary>
    public sealed class TaskItem
    {
        public string Id { get; set; }              // Guid("N")，复制时重新生成
        public string Name { get; set; }            // 用户起的名字，同一列表内唯一
        public string TypeKey { get; set; }         // "script" / "single-axis" / "glue" …
        public string Data { get; set; }            // 由任务类型解释；类型未注册时原样保留，不丢数据
        public bool Enabled { get; set; } = true;
        public bool PauseAfter { get; set; }        // 执行完暂停，等操作员继续
        public int TimeoutMs { get; set; }          // 0 = 不限
    }

    /// <summary>
    /// 任务类型：参数界面 + 校验 + 执行。任务系统里所有任务（含脚本任务）都是它的实现，彼此平级。
    /// Validate / Summary / CreateDefaultData 必须无副作用、不访问硬件。
    /// </summary>
    public interface ITaskType
    {
        string Key { get; }
        string DisplayName { get; }
        string Group { get; }
        string EditorView { get; }                  // Prism 导航视图名；null 表示没有专用界面
        string CreateDefaultData();
        string Summary(string data);                // 列表第二行摘要
        string Validate(string data);               // 通过返回 null，否则返回错误文本（可多行）
        /// <summary>取消令牌触发时必须尽快返回并让硬件停下；失败抛异常，消息显示给用户。</summary>
        Task ExecuteAsync(string data, IRunContext ctx, CancellationToken ct);
    }

    /// <summary>可选：能把自己的数据转换成另一种任务类型的数据（转换不可逆）。任务层只认识目标类型的 Key。</summary>
    public interface ITaskConvertible
    {
        string TargetTypeKey { get; }
        string Convert(string data);                // 数据非法抛 InvalidOperationException
    }

    public interface ITaskTypeRegistry
    {
        void Register(ITaskType type);              // Key 重复抛异常
        bool TryGet(string key, out ITaskType type);
        IReadOnlyList<ITaskType> All { get; }
    }

    /// <summary>选中任务时交给编辑界面的会话：读 Data、改完 Commit；标脏、审计、权限由列表一侧负责。</summary>
    public interface ITaskEditSession
    {
        string TaskId { get; }
        string Data { get; }
        bool ReadOnly { get; }                      // 运行中或权限不足
        event EventHandler ReadOnlyChanged;
        void Commit(string newData);                // 不抛异常，错误由列表提示
        void Convert();                             // 按 ITaskConvertible 转换成目标类型
    }

    public enum RunOutcome { Completed, Paused, Failed, Cancelled }

    public sealed class RunReport
    {
        public RunOutcome Outcome { get; set; }
        public string Error { get; set; }
        public string FailedTaskId { get; set; }
        public int Cycles { get; set; }
        public TimeSpan Elapsed { get; set; }
    }

    public sealed class TaskStartedEventArgs : EventArgs { public string TaskId { get; set; } }

    public sealed class TaskFinishedEventArgs : EventArgs
    {
        public string TaskId { get; set; }
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string Error { get; set; }
        public TimeSpan Elapsed { get; set; }
    }

    /// <summary>单步、整体、下一步都是同一个调用，传入要执行的任务即可。事件在调用 RunAsync 的线程触发。</summary>
    public interface ITaskRunner
    {
        bool IsRunning { get; }
        event EventHandler<TaskStartedEventArgs> TaskStarted;
        event EventHandler<TaskFinishedEventArgs> TaskFinished;
        event EventHandler RunningChanged;
        Task<RunReport> RunAsync(IReadOnlyList<TaskItem> tasks, bool loop);    // 已有运行时抛 InvalidOperationException
        void Stop(string reason);
    }
}

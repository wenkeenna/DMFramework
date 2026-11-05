using DM.Core.Common;
using DM.Core.TaskList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Engine.TaskList
{
    /// <summary>
    /// 可视化任务的基类：Data 是 TData 的 JSON。子类写三件事：
    /// Check（参数校验，可选）、RunAsync（执行，直接调用服务）、Describe（列表摘要）。
    /// </summary>
    public abstract class JsonTaskType<TData> : ITaskType where TData : class, new()
    {
        protected JsonTaskType(string key, string displayName, string group, string editorView)
        { Key = key; DisplayName = displayName; Group = group; EditorView = editorView; }

        public string Key { get; }
        public string DisplayName { get; }
        public string Group { get; }
        public string EditorView { get; }

        public string CreateDefaultData() { return TaskJson.Serialize(new TData()); }

        public string Summary(string data)
        {
            try { return Describe(Load(data)); }
            catch (Exception) { return "（数据无效）"; }
        }

        public string Validate(string data)
        {
            try { return Check(Load(data)); }
            catch (InvalidOperationException ex) { return ex.Message; }
        }

        public Task ExecuteAsync(string data, IRunContext ctx, CancellationToken ct) { return RunAsync(Load(data), ctx, ct); }

        protected virtual string Check(TData d) { return null; }
        protected abstract Task RunAsync(TData d, IRunContext ctx, CancellationToken ct);
        protected abstract string Describe(TData d);

        private static TData Load(string data) { return TaskJson.Deserialize<TData>(data); }
    }
}

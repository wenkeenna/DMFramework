using DM.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Core.Scripting
{
    /// <summary>参数规格。Type 支持 string/double/int/bool；Default 的类型必须与 Type 一致；可选参数排在必填之后。</summary>
    public sealed class ScriptParamSpec
    {
        public string Name { get; set; }
        public Type Type { get; set; } = typeof(string);
        public bool Required { get; set; } = true;
        public object Default { get; set; }
        public string Description { get; set; }
        public string[] Options { get; set; }       // 非空时只允许这些值（忽略大小写）
        public bool Positive { get; set; }          // 数值必须 > 0
        public bool AllowBinding { get; set; }      // 允许写成 @Root.Child
        public bool TakesRest { get; set; }         // 仅最后一个参数：吞掉其后所有词，用空格连接
        public bool IsDataKey { get; set; }         // 值是 Root.Child，命令会向它写结果
        public Func<IEnumerable<string>> Suggest { get; set; }   // 仅用于补全，不做强制校验
    }

    /// <summary>命令规格：校验、帮助、补全、高亮都从它生成，只写这一处。</summary>
    public sealed class ScriptCommandSpec
    {
        public string Name { get; set; }
        public string Group { get; set; }
        public string Description { get; set; }
        public ScriptParamSpec[] Params { get; set; } = new ScriptParamSpec[0];

        public string HelpText
        {
            get
            {
                var sb = new StringBuilder(Name);
                foreach (var p in Params)
                {
                    string n = p.AllowBinding ? p.Name + "|@Root.Child" : p.Name;
                    if (p.TakesRest) n += "…";
                    sb.Append(p.Required ? " " + n : " [" + n + "]");
                }
                return sb.ToString();
            }
        }
    }

    /// <summary>执行时交给命令的参数：绑定已解析、类型已转好。</summary>
    public sealed class ScriptArgs
    {
        private readonly Dictionary<string, object> _v;
        public ScriptArgs(Dictionary<string, object> values) { _v = values; }

        public T Get<T>(string name)
        {
            object o;
            if (!_v.TryGetValue(name, out o)) throw new KeyNotFoundException("脚本参数不存在：" + name);
            return o == null ? default(T) : (T)o;
        }
    }

    public interface IScriptCommand
    {
        ScriptCommandSpec Spec { get; }
        /// <summary>参数已按 Spec 校验和转换；取消时必须尽快返回（经服务让硬件停下）。</summary>
        Task ExecuteAsync(ScriptArgs args, IRunContext ctx, CancellationToken ct);
    }

    /// <summary>可选：运行前的跨参数校验。literals 只含字面量参数（@绑定的值运行前未知）。返回 null 表示通过。</summary>
    public interface IScriptCommandCheck { string Check(IReadOnlyDictionary<string, object> literals); }

    public interface IScriptCommandRegistry
    {
        void Register(IScriptCommand command);                  // 重名抛异常，避免扩展包悄悄覆盖内置命令
        bool TryGet(string name, out IScriptCommand command);
        IReadOnlyList<IScriptCommand> All { get; }
    }
}

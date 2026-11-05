using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Engine.TaskList
{
    /// <summary>任务数据统一的序列化设置：枚举存成文字，便于人读和审计对比。</summary>
    public static class TaskJson
    {
        private static readonly JsonSerializerSettings S = new JsonSerializerSettings
        { Formatting = Formatting.None, Converters = { new StringEnumConverter() } };

        public static string Serialize(object o) { return JsonConvert.SerializeObject(o, S); }

        public static T Deserialize<T>(string json) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(json)) return new T();
            try { return JsonConvert.DeserializeObject<T>(json, S) ?? new T(); }
            catch (JsonException ex) { throw new InvalidOperationException("任务数据损坏：" + ex.Message, ex); }
        }
    }
}

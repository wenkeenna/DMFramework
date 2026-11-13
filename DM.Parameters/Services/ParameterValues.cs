using DM.Core.Data;
using DM.Parameters.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Parameters.Services
{
    /// <summary>参数值的类型推断、解析、格式化和键校验。没有任何界面依赖。</summary>
    public static class ParameterValues
    {
        /// <summary>按值的真实类型推断。JSON 里的整数读回来是 long，小数是 double。</summary>
        public static ParamKind InferKind(object v)
        {
            if (v is bool) return ParamKind.Bool;
            if (v is int || v is long || v is short || v is byte || v is sbyte || v is uint || v is ushort || v is ulong) return ParamKind.Int;
            if (v is double || v is float || v is decimal) return ParamKind.Double;
            return ParamKind.String;
        }

        public static string Format(object v)
        {
            if (v == null) return "";
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);       // 保留完整精度，不取整
            if (v is float) return ((float)v).ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        }

        /// <summary>按指定类型解析。整数存成 long（与从文件读回来的类型一致，值相同时容器不会误判为变化）。</summary>
        public static bool TryParse(string text, ParamKind kind, out object value, out string error)
        {
            value = null; error = null; text = text ?? "";
            switch (kind)
            {
                case ParamKind.Int:
                    long l;
                    if (!long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
                    { error = "不是有效整数：" + text; return false; }
                    value = l; return true;

                case ParamKind.Double:
                    double d;       // 不允许千分位：输入 1,5 不会被悄悄当成 15
                    if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) || double.IsNaN(d) || double.IsInfinity(d))
                    { error = "不是有效数字：" + text; return false; }
                    value = d; return true;

                case ParamKind.Bool:
                    string u = text.Trim().ToUpperInvariant();
                    if (u == "1" || u == "ON" || u == "TRUE") { value = true; return true; }
                    if (u == "0" || u == "OFF" || u == "FALSE") { value = false; return true; }
                    error = "应为 true/false、ON/OFF 或 1/0：" + text; return false;

                default:
                    value = text; return true;          // 字符串原样保留，不去空格
            }
        }

        /// <summary>键必须是 Root.Child，且能在脚本里用 @Root.Child 引用（脚本按空白分词）。</summary>
        public static string CheckKey(string key)
        {
            key = key ?? "";
            string r, c;
            if (!DataKey.TrySplit(key, out r, out c)) return "键格式应为 Root.Child（两段都不能为空）。";
            if (key.Length > 100) return "键不能超过 100 个字符。";
            if (key.Any(ch => char.IsWhiteSpace(ch) || ch == '"' || ch == '@')) return "键不能包含空白字符、双引号或 @（脚本里无法引用）。";
            return null;
        }
    }
}

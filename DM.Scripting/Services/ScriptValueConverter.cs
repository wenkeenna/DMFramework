using DM.Core.Scripting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Scripting.Services
{
    /// <summary>字面量解析一律 Invariant，所有机器行为一致。</summary>
    public static class ScriptValueConverter
    {
        public static object Parse(ScriptParamSpec p, string text, out string error)
        {
            error = null;
            object v;
            Type t = p.Type;

            if (t == typeof(double))
            {
                double d;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) { error = "不是有效数字：" + text; return null; }
                v = d;
            }
            else if (t == typeof(int))
            {
                int n;
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) { error = "不是有效整数：" + text; return null; }
                v = n;
            }
            else if (t == typeof(bool))
            {
                string u = (text ?? "").Trim().ToUpperInvariant();
                if (u == "1" || u == "ON" || u == "TRUE") v = true;
                else if (u == "0" || u == "OFF" || u == "FALSE") v = false;
                else { error = "应为 ON/OFF、1/0 或 true/false：" + text; return null; }
            }
            else v = text;

            error = Validate(p, v);
            return error == null ? v : null;
        }

        /// <summary>范围与选项检查。编译期（字面量）和运行期（绑定值）共用。</summary>
        public static string Validate(ScriptParamSpec p, object v)
        {
            if (p.Positive && (p.Type == typeof(double) || p.Type == typeof(int))
                && Convert.ToDouble(v, CultureInfo.InvariantCulture) <= 0) return "必须大于 0。";

            if (p.Options != null && p.Options.Length > 0)
            {
                string s = Convert.ToString(v, CultureInfo.InvariantCulture);
                if (!p.Options.Contains(s, StringComparer.OrdinalIgnoreCase)) return "只能是：" + string.Join("/", p.Options);
            }
            return null;
        }
    }
}

using DM.Core.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Scripting
{
    public static class ScriptText
    {
        /// <summary>普通字符串参数；含空白、为空、以 # 或 // 开头时自动加引号。脚本语法无引号转义，所以内容不允许含双引号。</summary>
        public static string Tok(string s)
        {
            s = (s ?? "").Trim();
            if (s.IndexOf('"') >= 0) throw new InvalidOperationException("名称不能包含双引号：" + s);
            bool quote = s.Length == 0 || s.Any(char.IsWhiteSpace) || s.StartsWith("#") || s.StartsWith("//");
            return quote ? "\"" + s + "\"" : s;
        }

        public static string Quoted(string s)
        {
            if (s != null && s.IndexOf('"') >= 0) throw new InvalidOperationException("文本不能包含双引号：" + s);
            return "\"" + (s ?? "") + "\"";
        }

        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new InvalidOperationException("数值无效。");
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string OnOff(bool v) { return v ? "ON" : "OFF"; }

        public static string Bind(string key)
        {
            key = (key ?? "").Trim();
            string r, c;
            if (!DataKey.TrySplit(key, out r, out c)) throw new InvalidOperationException("绑定键格式应为 Root.Child：" + key);
            return "@" + key;
        }

        public static string Line(string command, params string[] args)
        { return args == null || args.Length == 0 ? command : command + " " + string.Join(" ", args); }
    }
}

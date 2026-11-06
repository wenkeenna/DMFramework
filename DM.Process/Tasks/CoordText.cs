using DM.Core.Data;
using DM.Process.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Process.Tasks
{
    /// <summary>文本框里的坐标：写数字，或写 @Root.Child 引用数据容器里的参数。</summary>
    public static class CoordText
    {
        public static string Check(string text, string what)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return what + "不能为空。";
            if (text.StartsWith("@"))
            { string r, c; return DataKey.TrySplit(text.Substring(1), out r, out c) ? null : what + "：绑定格式应为 @Root.Child。"; }
            double d;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? null : what + "：不是有效数字。";
        }

        public static double Resolve(string text, IDataContainer data)
        {
            text = (text ?? "").Trim();
            if (text.StartsWith("@")) return Read(data, text.Substring(1));
            double d;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) throw new InvalidOperationException("不是有效数字：" + text);
            return d;
        }

        public static double Read(IDataContainer data, string key)
        {
            double v;
            if (!data.TryGet<double>(key, out v)) throw new InvalidOperationException("读取数值失败：" + key);
            return v;
        }
    }

    public static class GlueJobs
    {
        /// <summary>Z 校准偏移 = 新值 − 基准值，取自数据容器；未启用校准为 0。</summary>
        public static double CalibZ(GlueSettings s, IDataContainer data)
        {
            if (!s.CalibrationEnabled) return 0;
            return CoordText.Read(data, s.CalibKey(s.CalibNewKey)) - CoordText.Read(data, s.CalibKey(s.CalibBaseKey));
        }

        public static GlueJob Make(GlueSettings s, GlueMode mode, GlueLineDirection dir, double x1, double y1, double x2, double y2, double calibZ)
        { return new GlueJob { Settings = s, Mode = mode, Direction = dir, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, CalibZ = calibZ }; }
    }
}

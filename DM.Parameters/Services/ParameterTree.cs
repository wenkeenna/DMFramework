using DM.Core.Data;
using DM.Parameters.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Parameters.Services
{
    public static class ParameterTree
    {
        /// <summary>
        /// 从容器快照建树。组按名字排序，组内叶子按名字排序。
        /// keepExpanded：刷新前已展开的组名，重建后恢复，避免每次刷新都全部收起。
        /// </summary>
        public static List<ParamGroupNode> Build(IEnumerable<KeyValuePair<string, object>> items, DataScope scope, ISet<string> keepExpanded)
        {
            var groups = new Dictionary<string, ParamGroupNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in items)
            {
                string r, c;
                if (!DataKey.TrySplit(kv.Key, out r, out c)) continue;           // 容器里的键都是合法的，防御性跳过
                ParamGroupNode g;
                if (!groups.TryGetValue(r, out g)) groups[r] = g = new ParamGroupNode(r);
                g.Children.Add(new ParamLeafNode(r, c, scope, kv.Value));
            }

            var result = groups.Values.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            foreach (ParamGroupNode g in result)
            {
                var sorted = g.Children.OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                g.Children.Clear();
                foreach (ParamLeafNode l in sorted) g.Children.Add(l);
                g.IsExpanded = keepExpanded != null && keepExpanded.Contains(g.Name);
            }
            return result;
        }

        public static ParamGroupNode FindGroup(IEnumerable<ParamGroupNode> groups, string root)
        { return groups.FirstOrDefault(g => string.Equals(g.Name, root, StringComparison.OrdinalIgnoreCase)); }

        public static ParamLeafNode FindLeaf(IEnumerable<ParamGroupNode> groups, string key)
        { return groups.SelectMany(g => g.Children).FirstOrDefault(l => string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase)); }

        /// <summary>
        /// 过滤：叶子的键或值包含关键字才显示；组下有命中的叶子才显示，并自动展开；
        /// 关键字为空时全部显示，展开状态保持不动。
        /// </summary>
        public static int ApplyFilter(IEnumerable<ParamGroupNode> groups, string filter)
        {
            string f = (filter ?? "").Trim();
            int hits = 0;
            foreach (ParamGroupNode g in groups)
            {
                if (f.Length == 0)
                {
                    g.IsVisible = true;
                    foreach (ParamLeafNode l in g.Children) l.IsVisible = true;
                    continue;
                }
                int n = 0;
                foreach (ParamLeafNode l in g.Children)
                {
                    l.IsVisible = l.Key.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                               || l.Display.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (l.IsVisible) n++;
                }
                g.IsVisible = n > 0;
                if (n > 0) g.IsExpanded = true;
                hits += n;
            }
            return hits;
        }

        /// <summary>重命名后叶子要从旧组移到新组；新组不存在就建（按排序位置插入，保持树有序）。</summary>
        public static ParamGroupNode EnsureGroup(IList<ParamGroupNode> groups, string root)
        {
            ParamGroupNode g = FindGroup(groups, root);
            if (g != null) return g;
            g = new ParamGroupNode(root);
            int i = 0;
            while (i < groups.Count && string.Compare(groups[i].Name, root, StringComparison.CurrentCultureIgnoreCase) < 0) i++;
            groups.Insert(i, g);
            return g;
        }
    }
}

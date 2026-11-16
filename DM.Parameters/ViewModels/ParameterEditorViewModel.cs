using DM.Core.Audit;
using DM.Core.Data;
using DM.Core.Identity;
using DM.Core.Projects;
using DM.Core.TaskList;
using DM.Parameters.Models;
using DM.Parameters.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using DM.UI;

namespace DM.Parameters.ViewModels
{
    /// <summary>
    /// 树形参数编辑窗口。修改直接写进 IDataContainer：
    ///   · 值修改、新增、删除的审计由 DataAuditBridge 订阅容器变化自动记录（旧值 → 新值），这里不重复记；
    ///   · 重命名在容器里是"新增新键 + 删除旧键"，会产生成对的记录，这里额外写一条 Rename 汇总，便于追溯；
    ///   · 值没变化不写入；
    ///   · 关闭窗口时若改过，统一走项目保存（与主窗口"保存"按钮同一条路径）。
    /// 只读条件：未打开项目、任务运行中、角色低于 Engineer、选择的是运行时范围。
    /// </summary>
    public sealed class ParameterEditorViewModel : BindableBase, IDialogAware
    {
        public static readonly AccessRole EditRole = AccessRole.Engineer;

        private readonly IDataContainer _data;
        private readonly ICurrentUser _user;
        private readonly IProjectLifecycle _life;
        private readonly ITaskRunner _runner;
        private readonly IAuditService _audit;
        private readonly ITaskReferenceFinder _refs;
        private ScopeItem _scope;
        private ParamNode _selected;
        private string _filter = "", _status = "", _newGroup = "", _newChild = "", _newValue = "0";
        private ParamKind _newKind = ParamKind.Double;
        private bool _readOnly;
        private int _changes;

        public ParameterEditorViewModel(IDataContainer data, ICurrentUser user, IProjectLifecycle life, ITaskRunner runner,
                                        IAuditService audit, ITaskReferenceFinder refs)
        {
            _data = data; _user = user; _life = life; _runner = runner; _audit = audit; _refs = refs;

            Scopes = new[]
            {
                new ScopeItem(DataScope.Project, "项目参数", true),
                new ScopeItem(DataScope.Machine, "设备参数（标定值等）", true),
                new ScopeItem(DataScope.Runtime, "运行时（只读）", false)
            };
            _scope = Scopes[0];
            Kinds = (ParamKind[])Enum.GetValues(typeof(ParamKind));

            RefreshCommand = new DelegateCommand(Refresh);
            AddCommand = new DelegateCommand(OnAdd, () => CanEdit).ObservesProperty(() => CanEdit);
            DeleteCommand = new DelegateCommand(OnDelete, () => CanEdit && Selected != null).ObservesProperty(() => CanEdit).ObservesProperty(() => Selected);
            RenameCommand = new DelegateCommand(OnRename, () => CanEdit && Selected != null).ObservesProperty(() => CanEdit).ObservesProperty(() => Selected);
            ExpandAllCommand = new DelegateCommand(() => SetExpanded(true));
            CollapseAllCommand = new DelegateCommand(() => SetExpanded(false));
            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK)));
        }

        public string Title => "项目参数";
        public event Action<IDialogResult> RequestClose;

        public ObservableCollection<ParamGroupNode> Groups { get; } = new ObservableCollection<ParamGroupNode>();
        public ScopeItem[] Scopes { get; }
        public ParamKind[] Kinds { get; }

        public ScopeItem SelectedScope
        {
            get { return _scope; }
            set { if (SetProperty(ref _scope, value)) { RaisePropertyChanged(nameof(CanEdit)); Refresh(); } }
        }

        /// <summary>树的选中项（视图在 SelectedItemChanged 里同步）。</summary>
        public ParamNode Selected
        {
            get { return _selected; }
            set
            {
                if (!SetProperty(ref _selected, value)) return;
                RaisePropertyChanged(nameof(SelectedText));
                if (value is ParamGroupNode) NewGroup = value.Name;            // 选中组时，新增区的"组"自动填上
                else if (value is ParamLeafNode) NewGroup = ((ParamLeafNode)value).Root;
            }
        }
        public string SelectedText => _selected == null ? "" : _selected is ParamLeafNode ? ((ParamLeafNode)_selected).Key : _selected.Name + ".*";

        public string Filter
        {
            get { return _filter; }
            set
            {
                if (!SetProperty(ref _filter, value ?? "")) return;
                int hits = ParameterTree.ApplyFilter(Groups, _filter);
                if (_filter.Trim().Length > 0) Status = "找到 " + hits + " 项匹配。";
            }
        }
        public string Status { get { return _status; } private set { SetProperty(ref _status, value); } }
        public string CountText => "共 " + Groups.Sum(g => g.Children.Count) + " 项，" + Groups.Count + " 组";

        public bool ReadOnly => _readOnly;
        public bool CanEdit => !_readOnly && _scope.Editable;

        public string NewGroup { get { return _newGroup; } set { SetProperty(ref _newGroup, value ?? ""); } }
        public string NewChild { get { return _newChild; } set { SetProperty(ref _newChild, value ?? ""); } }
        public ParamKind NewKind { get { return _newKind; } set { SetProperty(ref _newKind, value); } }
        public string NewValue { get { return _newValue; } set { SetProperty(ref _newValue, value ?? ""); } }

        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand RenameCommand { get; }
        public DelegateCommand ExpandAllCommand { get; }
        public DelegateCommand CollapseAllCommand { get; }
        public DelegateCommand CloseCommand { get; }

        // ───── 查询 ─────
        private void Refresh()
        {
            // 记住展开状态和选中项，重建后恢复
            var expanded = new HashSet<string>(Groups.Where(g => g.IsExpanded).Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
            string selKey = _selected == null ? null : _selected is ParamLeafNode ? ((ParamLeafNode)_selected).Key : _selected.Name;
            bool selIsLeaf = _selected is ParamLeafNode;

            Groups.Clear();
            foreach (ParamGroupNode g in ParameterTree.Build(_data.Snapshot(_scope.Scope), _scope.Scope, expanded)) Groups.Add(g);
            ParameterTree.ApplyFilter(Groups, _filter);

            ParamNode restore = null;
            if (selKey != null) restore = selIsLeaf ? (ParamNode)ParameterTree.FindLeaf(Groups, selKey) : ParameterTree.FindGroup(Groups, selKey);
            Selected = restore;
            if (restore != null) restore.IsSelected = true;
            RaisePropertyChanged(nameof(CountText));
        }

        private void SetExpanded(bool v) { foreach (ParamGroupNode g in Groups) g.IsExpanded = v; }

        // ───── 行内编辑（由视图调用）─────
        public bool CanEditLeaf(ParamLeafNode leaf) { return !_readOnly && leaf != null && leaf.Scope != DataScope.Runtime; }
        public void Report(string message) { Status = message; }

        public void BeginEdit(ParamLeafNode leaf)
        {
            if (!CanEditLeaf(leaf) || leaf.IsBool) return;                 // 布尔值直接点复选框
            leaf.EditText = leaf.Display;
            leaf.IsEditing = true;
        }

        public void CancelEdit(ParamLeafNode leaf)
        {
            if (leaf == null) return;
            leaf.EditText = leaf.Display; leaf.IsEditing = false;
        }

        /// <summary>提交一次编辑。失败时返回 false 并给出原因，视图据此让文本框保持在编辑状态。</summary>
        public bool TryApply(ParamLeafNode leaf, string text, out string error)
        {
            error = null;
            if (!CanEditLeaf(leaf)) { error = "当前不可编辑。"; return false; }
            if (!CheckRole("Edit", leaf.Key, out error)) return false;

            object v;
            if (!ParameterValues.TryParse(text, leaf.Kind, out v, out error)) return false;
            if (ParameterValues.Format(v) == leaf.Display) { leaf.IsEditing = false; return true; }      // 没变化：不写入，不产生审计

            try { _data.Set(leaf.Key, v, leaf.Scope); }
            catch (Exception ex) { error = "写入失败：" + ex.Message; return false; }

            leaf.SetValue(v);
            leaf.IsEditing = false;
            _changes++;
            Status = "已修改 " + leaf.Key + " = " + leaf.Display;
            return true;
        }

        /// <summary>布尔参数的复选框直接切换。</summary>
        public void ToggleBool(ParamLeafNode leaf, bool value)
        {
            string error;
            if (!TryApply(leaf, value ? "true" : "false", out error)) { Status = error; leaf.SetValue(leaf.Value); }   // 失败时让复选框回到原值
        }

        // ───── 新增 ─────
        private void OnAdd()
        {
            string error, key = NewGroup.Trim() + "." + NewChild.Trim();
            if (!CheckRole("Add", key, out error)) { Status = error; return; }

            error = ParameterValues.CheckKey(key);
            if (error == null && _data.Contains(key)) error = "参数已存在：" + key;          // 容器的键在所有作用域内唯一
            object v = null;
            if (error == null) ParameterValues.TryParse(NewValue, NewKind, out v, out error);
            if (error != null) { Status = error; return; }

            try { _data.Set(key, v, _scope.Scope); }
            catch (Exception ex) { Status = "写入失败：" + ex.Message; return; }

            ParamGroupNode g = ParameterTree.EnsureGroup(Groups, NewGroup.Trim());
            var leaf = new ParamLeafNode(g.Name, NewChild.Trim(), _scope.Scope, v);
            InsertSorted(g, leaf);
            g.IsExpanded = true; g.RefreshCount();
            leaf.IsSelected = true; Selected = leaf;
            _changes++;
            NewChild = "";
            RaisePropertyChanged(nameof(CountText));
            Status = "已新增 " + key + " = " + leaf.Display;
        }

        private static void InsertSorted(ParamGroupNode g, ParamLeafNode leaf)
        {
            int i = 0;
            while (i < g.Children.Count && string.Compare(g.Children[i].Name, leaf.Name, StringComparison.CurrentCultureIgnoreCase) < 0) i++;
            g.Children.Insert(i, leaf);
        }

        // ───── 删除 ─────
        private void OnDelete()
        {
            ParamNode node = _selected;
            if (node == null) return;
            var group = node as ParamGroupNode;
            var leaf = node as ParamLeafNode;

            string error, target = leaf != null ? leaf.Key : group.Name + ".*";
            if (!CheckRole("Remove", target, out error)) { Status = error; return; }

            string refKey = leaf != null ? leaf.Key : group.Name;
            IReadOnlyList<string> used = _refs.FindReferences(refKey, group != null);
            string text = (group != null ? "确定删除整组 " + group.Name + "（" + group.Children.Count + " 个参数）吗？" : "确定删除参数 " + leaf.Key + " 吗？")
                        + RefText(used) + "\n删除后引用它的任务运行时会报“读取数值失败”。";
            if (MessageBox.Show(text, Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

            try
            {
                if (leaf != null) { _data.Remove(leaf.Key); Remove(leaf); }
                else
                {
                    foreach (ParamLeafNode l in group.Children.ToList()) { _data.Remove(l.Key); }
                    Groups.Remove(group);
                }
            }
            catch (Exception ex) { Status = "删除失败：" + ex.Message; Refresh(); return; }       // 已删的部分已写进容器，重建视图保持一致

            _changes++;
            Selected = null;
            RaisePropertyChanged(nameof(CountText));
            Status = "已删除 " + target;
        }

        private void Remove(ParamLeafNode leaf)
        {
            ParamGroupNode g = ParameterTree.FindGroup(Groups, leaf.Root);
            if (g == null) return;
            g.Children.Remove(leaf); g.RefreshCount();
            if (g.Children.Count == 0) Groups.Remove(g);                    // 空组消失，与容器的结构一致
        }

        // ───── 重命名 ─────
        private void OnRename()
        {
            ParamNode node = _selected;
            if (node == null) return;
            var leaf = node as ParamLeafNode;
            var group = node as ParamGroupNode;

            string current = leaf != null ? leaf.Name : group.Name;
            string input = Prompt.Ask(leaf != null ? "重命名参数（只改“参数名”，组不变）" : "重命名组（整组一起改）", current);
            if (input == null) return;
            input = input.Trim();
            if (input == current) return;

            string error;
            if (!CheckRole("Rename", current, out error)) { Status = error; return; }

            if (leaf != null) RenameLeaf(leaf, input); else RenameGroup(group, input);
        }

        private void RenameLeaf(ParamLeafNode leaf, string newChild)
        {
            string oldKey = leaf.Key, newKey = leaf.Root + "." + newChild;
            string error = ParameterValues.CheckKey(newKey);
            if (error == null && _data.Contains(newKey)) error = "参数已存在：" + newKey;
            if (error != null) { Status = error; return; }

            if (!ConfirmRename(oldKey, newKey, _refs.FindReferences(oldKey, false))) return;

            try
            {
                _data.Set(newKey, leaf.Value, leaf.Scope);          // 先建新键，再删旧键：中途失败也不会丢数据
                _data.Remove(oldKey);
            }
            catch (Exception ex) { Status = "重命名失败：" + ex.Message; Refresh(); return; }

            _audit.Record(AuditCategory.Parameter, "Rename", oldKey, oldKey, newKey, detail: "Scope=" + leaf.Scope);
            _changes++;
            Refresh();
            SelectKey(newKey);
            Status = "已重命名 " + oldKey + " → " + newKey + "。引用旧键的任务需要手工修改。";
        }

        private void RenameGroup(ParamGroupNode group, string newRoot)
        {
            string oldRoot = group.Name;
            string error = ParameterValues.CheckKey(newRoot + ".x");                // 借用键校验检查组名是否合法
            if (error == null && newRoot.Contains(".")) error = "组名不能包含点。";
            if (error != null) { Status = error.Replace("键", "组名"); return; }

            // 目标键已存在（含合并到已有组时的同名叶子）一律拒绝，不做覆盖
            var clash = group.Children.Select(l => newRoot + "." + l.Name).FirstOrDefault(k => _data.Contains(k));
            if (clash != null) { Status = "目标键已存在：" + clash; return; }

            if (!ConfirmRename(oldRoot + ".*", newRoot + ".*", _refs.FindReferences(oldRoot, true))) return;

            int done = 0;
            try
            {
                foreach (ParamLeafNode l in group.Children.ToList())
                {
                    _data.Set(newRoot + "." + l.Name, l.Value, l.Scope);
                    _data.Remove(l.Key);
                    done++;
                }
            }
            catch (Exception ex) { Status = "重命名失败（已改 " + done + " 项）：" + ex.Message; Refresh(); return; }

            _audit.Record(AuditCategory.Parameter, "RenameGroup", oldRoot, oldRoot, newRoot, detail: done + " 项");
            _changes++;
            Refresh();
            ParamGroupNode g = ParameterTree.FindGroup(Groups, newRoot);
            if (g != null) { g.IsSelected = true; Selected = g; g.IsExpanded = true; }
            Status = "已重命名组 " + oldRoot + " → " + newRoot + "（" + done + " 项）。引用它的任务需要手工修改。";
        }

        private bool ConfirmRename(string from, string to, IReadOnlyList<string> used)
        {
            string text = "把 " + from + " 重命名为 " + to + "？" + RefText(used) +
                          "\n任务和脚本里写的键不会自动替换，引用旧键的任务运行前预检会报错，需要你手工改。";
            return MessageBox.Show(text, Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }

        private static string RefText(IReadOnlyList<string> used)
        {
            if (used == null || used.Count == 0) return "\n\n没有任务引用它（脚本任务或可视化任务）。";
            string shown = string.Join("、", used.Take(8)) + (used.Count > 8 ? " 等 " + used.Count + " 个" : "");
            return "\n\n引用它的任务：" + shown + "。";
        }

        private void SelectKey(string key)
        {
            ParamLeafNode leaf = ParameterTree.FindLeaf(Groups, key);
            if (leaf == null) return;
            ParamGroupNode g = ParameterTree.FindGroup(Groups, leaf.Root);
            if (g != null) g.IsExpanded = true;
            leaf.IsSelected = true; Selected = leaf;
        }

        /// <summary>界面已按角色置只读，这里是第二道防线：越权尝试要留下 Denied 记录。</summary>
        private bool CheckRole(string action, string target, out string error)
        {
            error = null;
            if (_user.Role >= EditRole) return true;
            _audit.Record(AuditCategory.Parameter, action, target, result: AuditResult.Denied, detail: "需要 " + EditRole + " 及以上角色");
            error = "权限不足：修改参数需要工程师及以上权限。";
            return false;
        }

        // ───── IDialogAware ─────
        public bool CanCloseDialog() { return true; }

        public void OnDialogOpened(IDialogParameters p)
        {
            string note = null;
            if (_life.Current == null) { _readOnly = true; note = "请先新建或打开项目。"; }
            else if (_runner.IsRunning) { _readOnly = true; note = "任务运行中，参数只读。"; }
            else if (_user.Role < EditRole) { _readOnly = true; note = "当前账号只能查看参数，修改需要工程师及以上权限。"; }

            RaisePropertyChanged(nameof(ReadOnly));
            RaisePropertyChanged(nameof(CanEdit));
            Refresh();
            Status = note ?? "选中参数后按 F2 或双击值编辑，回车确认，Esc 取消。修改立即生效并记入审计，关闭窗口时自动保存到项目。";
        }

        /// <summary>无论怎样关闭（按钮、×、Esc）都走这里：改过才保存。保存与"保存"按钮同一路径，失败会提示。</summary>
        public async void OnDialogClosed()
        {
            if (_changes == 0) return;
            try
            {
                var fails = await _life.SaveAsync();
                if (fails.Count > 0)
                    MessageBox.Show("参数已修改，但保存失败：\n" + string.Join("\n", fails.Select(f => f.Name + "：" + f.Error.Message)),
                                    Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            { MessageBox.Show("参数已修改，但保存失败：" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}

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

namespace DM.Parameters.ViewModels
{
    /// <summary>
    /// 参数编辑窗口。修改直接写进 IDataContainer：
    ///   · 审计由 DataAuditBridge 订阅容器变化自动记录（旧值 → 新值），这里不重复记；
    ///   · 值没变化不写入，不产生审计；
    ///   · 关闭窗口时若改过，统一走项目保存（与"保存"按钮同一条路径，落盘到 params.json / machine-data.json）。
    /// 只读条件：未打开项目、任务运行中、角色低于 Engineer、选择的是运行时作用域。
    /// </summary>
    public sealed class ParameterEditorViewModel : BindableBase, IDialogAware
    {
        public static readonly AccessRole EditRole = AccessRole.Engineer;

        private readonly IDataContainer _data;
        private readonly ICurrentUser _user;
        private readonly IProjectLifecycle _life;
        private readonly ITaskRunner _runner;
        private readonly IAuditService _audit;
        private ScopeItem _scope;
        private ParameterRow _selected;
        private string _filter = "", _status = "", _newKey = "", _newValue = "0";
        private ParamKind _newKind = ParamKind.Double;
        private bool _readOnly;
        private int _changes;

        public ParameterEditorViewModel(IDataContainer data, ICurrentUser user, IProjectLifecycle life, ITaskRunner runner, IAuditService audit)
        {
            _data = data; _user = user; _life = life; _runner = runner; _audit = audit;

            Scopes = new[]
            {
                new ScopeItem(DataScope.Project, "项目参数", true),
                new ScopeItem(DataScope.Machine, "设备参数（标定值等）", true),
                new ScopeItem(DataScope.Runtime, "运行时（只读）", false)
            };
            _scope = Scopes[0];
            Kinds = (ParamKind[])Enum.GetValues(typeof(ParamKind));

            View = CollectionViewSource.GetDefaultView(Rows);
            View.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ParameterRow.Root)));
            View.SortDescriptions.Add(new SortDescription(nameof(ParameterRow.Root), ListSortDirection.Ascending));
            View.SortDescriptions.Add(new SortDescription(nameof(ParameterRow.Child), ListSortDirection.Ascending));
            View.Filter = o => Match((ParameterRow)o);
            Rows.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(CountText));

            RefreshCommand = new DelegateCommand(Refresh);
            AddCommand = new DelegateCommand(OnAdd, () => CanEdit).ObservesProperty(() => CanEdit);
            DeleteCommand = new DelegateCommand(OnDelete, () => CanEdit && Selected != null)
                .ObservesProperty(() => CanEdit).ObservesProperty(() => Selected);
            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK)));
        }

        public string Title => "项目参数";
        public event Action<IDialogResult> RequestClose;

        public ObservableCollection<ParameterRow> Rows { get; } = new ObservableCollection<ParameterRow>();
        public ICollectionView View { get; }
        public ScopeItem[] Scopes { get; }
        public ParamKind[] Kinds { get; }

        public ScopeItem SelectedScope
        {
            get { return _scope; }
            set { if (SetProperty(ref _scope, value)) { RaisePropertyChanged(nameof(CanEdit)); Refresh(); } }
        }
        public ParameterRow Selected { get { return _selected; } set { SetProperty(ref _selected, value); } }
        public string Filter { get { return _filter; } set { if (SetProperty(ref _filter, value ?? "")) View.Refresh(); } }
        public string Status { get { return _status; } private set { SetProperty(ref _status, value); } }
        public string CountText => "共 " + Rows.Count + " 项";

        public bool ReadOnly => _readOnly;
        public bool CanEdit => !_readOnly && _scope.Editable;

        public string NewKey { get { return _newKey; } set { SetProperty(ref _newKey, value ?? ""); } }
        public ParamKind NewKind { get { return _newKind; } set { SetProperty(ref _newKind, value); } }
        public string NewValue { get { return _newValue; } set { SetProperty(ref _newValue, value ?? ""); } }

        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand CloseCommand { get; }

        // ───── 查询 ─────
        private void Refresh()
        {
            Rows.Clear();
            foreach (var kv in _data.Snapshot(_scope.Scope)) Rows.Add(new ParameterRow(kv.Key, _scope.Scope, kv.Value));
            Selected = null;
        }

        private bool Match(ParameterRow r)
        {
            string f = _filter.Trim();
            return f.Length == 0
                || r.Key.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0
                || r.Display.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ───── 编辑（由视图的 DataGrid 单元格编辑事件调用）─────
        public bool CanEditRow(ParameterRow row) { return !_readOnly && row != null && row.Scope != DataScope.Runtime; }

        public void Report(string message) { Status = message; }

        /// <summary>提交一次单元格编辑。失败时返回 false 并给出原因，视图据此让单元格停留在编辑状态。</summary>
        public bool TryApply(ParameterRow row, string text, out string error)
        {
            error = null;
            if (!CanEditRow(row)) { error = "当前不可编辑。"; return false; }
            if (!CheckRole("Edit", row.Key, out error)) return false;

            object v;
            if (!ParameterValues.TryParse(text, row.Kind, out v, out error)) return false;
            if (ParameterValues.Format(v) == row.Display) return true;          // 没变化：不写入，不产生审计

            try { _data.Set(row.Key, v, row.Scope); }
            catch (Exception ex) { error = "写入失败：" + ex.Message; return false; }

            row.SetValue(v);
            _changes++;
            Status = "已修改 " + row.Key + " = " + row.Display;
            return true;
        }

        private void OnAdd()
        {
            string error, key = NewKey.Trim();
            if (!CheckRole("Add", key, out error)) { Status = error; return; }

            error = ParameterValues.CheckKey(key);
            if (error == null && _data.Contains(key)) error = "参数已存在：" + key;     // 容器键在所有作用域内唯一
            object v = null;
            if (error == null) ParameterValues.TryParse(NewValue, NewKind, out v, out error);
            if (error != null) { Status = error; return; }

            try { _data.Set(key, v, _scope.Scope); }
            catch (Exception ex) { Status = "写入失败：" + ex.Message; return; }

            var row = new ParameterRow(key, _scope.Scope, v);
            Rows.Add(row);
            Selected = row;
            _changes++;
            NewKey = "";
            Status = "已新增 " + key + " = " + row.Display;
        }

        private void OnDelete()
        {
            ParameterRow row = Selected;
            if (row == null) return;

            string error;
            if (!CheckRole("Remove", row.Key, out error)) { Status = error; return; }
            if (MessageBox.Show("确定删除参数 " + row.Key + " 吗？\n引用它的任务运行时会报“读取数值失败”。",
                                Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

            try { _data.Remove(row.Key); }
            catch (Exception ex) { Status = "删除失败：" + ex.Message; return; }

            Rows.Remove(row);
            _changes++;
            Status = "已删除 " + row.Key;
        }

        /// <summary>界面已经按角色置只读，这里是第二道防线：越权尝试要留下 Denied 记录。</summary>
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
            Status = note ?? "双击或按 F2 编辑，回车确认，Esc 取消。修改立即生效并记入审计，关闭窗口时自动保存到项目。";
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

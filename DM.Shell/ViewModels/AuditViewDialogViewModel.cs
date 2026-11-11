using DM.Core.Audit;
using DM.Core.Identity;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Shell.ViewModels
{
    public sealed class CategoryItem { public string Name { get; set; } public AuditCategory? Value { get; set; } }

    public sealed class AuditRow
    {
        public AuditRow(AuditEntry e)
        {
            TimeText = e.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            User = e.UserName; Role = e.Role; Project = e.ProjectName; Action = e.Action; Target = e.Target;
            Category = AuditViewDialogViewModel.CategoryName(e.Category);
            Result = e.Result; Old = e.OldValue; New = e.NewValue; Detail = e.Detail; TraceId = e.TraceId; Machine = e.Machine;
        }
        public string TimeText { get; }
        public string User { get; }
        public string Role { get; }
        public string Project { get; }
        public string Category { get; }
        public string Action { get; }
        public string Target { get; }
        public AuditResult Result { get; }
        public string Old { get; }
        public string New { get; }
        public string Detail { get; }
        public string TraceId { get; }
        public string Machine { get; }
    }

    public sealed class AuditViewDialogViewModel : BindableBase, IDialogAware
    {
        private static readonly Dictionary<AuditCategory, string> Names = new Dictionary<AuditCategory, string>
        {
            { AuditCategory.System, "系统" }, { AuditCategory.Account, "账号" }, { AuditCategory.Project, "项目" },
            { AuditCategory.Parameter, "参数" }, { AuditCategory.TaskList, "任务列表" }, { AuditCategory.Operation, "手动操作" },
            { AuditCategory.Run, "运行" }, { AuditCategory.License, "授权" }, { AuditCategory.Alarm, "报警" }
        };

        private readonly IAuditReader _reader;
        private readonly ICurrentUser _user;
        private readonly IAuditService _audit;
        private DateTime? _from = DateTime.Today.AddDays(-7), _to = DateTime.Today;
        private CategoryItem _category;
        private string _userFilter = "", _text = "", _status = "";
        private bool _busy, _allowed;
        private AuditRow _selected;

        public AuditViewDialogViewModel(IAuditReader reader, ICurrentUser user, IAuditService audit)
        {
            _reader = reader; _user = user; _audit = audit;
            Categories = new[] { new CategoryItem { Name = "全部", Value = null } }
                .Concat(Enum.GetValues(typeof(AuditCategory)).Cast<AuditCategory>().Select(c => new CategoryItem { Name = CategoryName(c), Value = c })).ToList();
            _category = Categories[0];

            SearchCommand = new DelegateCommand(OnSearch, () => _allowed && !Busy).ObservesProperty(() => Busy);
            VerifyCommand = new DelegateCommand(OnVerify, () => _allowed && !Busy).ObservesProperty(() => Busy);
            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK)));
        }

        public static string CategoryName(AuditCategory c) { string n; return Names.TryGetValue(c, out n) ? n : c.ToString(); }

        public string Title => "审计日志";
        public event Action<IDialogResult> RequestClose;

        public IReadOnlyList<CategoryItem> Categories { get; }
        public ObservableCollection<AuditRow> Rows { get; } = new ObservableCollection<AuditRow>();
        public DateTime? FromDate { get { return _from; } set { SetProperty(ref _from, value); } }
        public DateTime? ToDate { get { return _to; } set { SetProperty(ref _to, value); } }
        public CategoryItem Category { get { return _category; } set { SetProperty(ref _category, value); } }
        public string UserFilter { get { return _userFilter; } set { SetProperty(ref _userFilter, value ?? ""); } }
        public string Text { get { return _text; } set { SetProperty(ref _text, value ?? ""); } }
        public string Status { get { return _status; } private set { SetProperty(ref _status, value); } }
        public bool Busy { get { return _busy; } private set { SetProperty(ref _busy, value); } }

        public AuditRow Selected
        {
            get { return _selected; }
            set { if (SetProperty(ref _selected, value)) RaisePropertyChanged(nameof(DetailText)); }
        }
        public string DetailText => _selected == null ? "" :
            "对象：" + _selected.Target + "\n旧值：\n" + _selected.Old + "\n\n新值：\n" + _selected.New +
            "\n\n说明：" + _selected.Detail + "\n运行编号：" + _selected.TraceId + "\n机器：" + _selected.Machine;

        public DelegateCommand SearchCommand { get; }
        public DelegateCommand VerifyCommand { get; }
        public DelegateCommand CloseCommand { get; }

        private async void OnSearch()
        {
            Busy = true; Status = "查询中…";
            try
            {
                var q = new AuditQuery
                {
                    FromUtc = StartUtc(FromDate),
                    ToUtc = EndUtc(ToDate),
                    Category = Category == null ? null : Category.Value,
                    User = UserFilter.Trim(),
                    Text = Text.Trim(),
                    Limit = 1000
                };
                IReadOnlyList<AuditEntry> list = await Task.Run(() => _reader.Query(q));
                Rows.Clear();
                foreach (AuditEntry e in list) Rows.Add(new AuditRow(e));
                Status = "共 " + list.Count + " 条" + (list.Count >= q.Limit ? "（已达显示上限，请缩小时间范围或加条件）" : "");
            }
            catch (Exception ex) { Status = "查询失败：" + ex.Message; }
            finally { Busy = false; }
        }

        private async void OnVerify()
        {
            Busy = true; Status = "正在校验哈希链…";
            try
            {
                AuditVerifyResult r = await Task.Run(() => _reader.Verify());
                Status = r.Message;
                _audit.Record(AuditCategory.System, "VerifyAuditChain", null, result: r.Ok ? AuditResult.Success : AuditResult.Failed, detail: r.Message);
            }
            catch (Exception ex) { Status = "校验失败：" + ex.Message; }
            finally { Busy = false; }
        }

        // 界面选的是本地日期，库里是 UTC：起始取当天 0 点，结束取当天最后一刻
        private static DateTime? StartUtc(DateTime? d)
        { return d.HasValue ? DateTime.SpecifyKind(d.Value.Date, DateTimeKind.Local).ToUniversalTime() : (DateTime?)null; }

        private static DateTime? EndUtc(DateTime? d)
        { return d.HasValue ? DateTime.SpecifyKind(d.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime() : (DateTime?)null; }

        public bool CanCloseDialog() { return true; }
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters p)
        {
            _allowed = _user.Role >= AccessRole.Engineer;
            SearchCommand.RaiseCanExecuteChanged(); VerifyCommand.RaiseCanExecuteChanged();
            if (!_allowed) { Status = "查看审计日志需要工程师及以上权限。"; return; }
            OnSearch();
        }
    }
}

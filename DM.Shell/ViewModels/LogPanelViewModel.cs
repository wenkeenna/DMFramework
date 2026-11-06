using DM.Core.Events;
using DM.Core.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;

namespace DM.Shell.ViewModels
{
    public sealed class LogRow
    {
        public string TimeText { get; set; }
        public LogLevel Level { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }         // 只取第一行，完整内容在提示里
        public string Detail { get; set; }
    }

    /// <summary>
    /// 日志发布可能来自任何线程、速度很快：先进并发队列，由 200ms 的定时器分批搬进界面集合，
    /// 一次最多 500 条，保留最近 2000 条。队列积压超过 5000 条时丢弃新日志（文件日志不受影响）。
    /// </summary>
    public sealed class LogPanelViewModel : BindableBase
    {
        private const int Max = 2000, MaxBatch = 500, MaxPending = 5000;
        private readonly ConcurrentQueue<LogEventMessage> _queue = new ConcurrentQueue<LogEventMessage>();
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private int _pending;
        private LogLevel _minLevel = LogLevel.Info;
        private bool _autoScroll = true;

        public LogPanelViewModel(IEventAggregator events)
        {
            View = CollectionViewSource.GetDefaultView(Rows);
            View.Filter = o => ((LogRow)o).Level >= _minLevel;
            ClearCommand = new DelegateCommand(Rows.Clear);

            events.GetEvent<AppLogEvent>().Subscribe(Enqueue, ThreadOption.PublisherThread, true);
            _timer.Tick += (s, e) => Drain();
            _timer.Start();
        }

        public ObservableCollection<LogRow> Rows { get; } = new ObservableCollection<LogRow>();
        public ICollectionView View { get; }
        public LogLevel[] Levels { get; } = { LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error };
        public DelegateCommand ClearCommand { get; }

        public LogLevel MinLevel { get { return _minLevel; } set { if (SetProperty(ref _minLevel, value)) View.Refresh(); } }
        public bool AutoScroll { get { return _autoScroll; } set { SetProperty(ref _autoScroll, value); } }

        private void Enqueue(LogEventMessage m)
        {
            if (m == null || Volatile.Read(ref _pending) > MaxPending) return;
            Interlocked.Increment(ref _pending);
            _queue.Enqueue(m);
        }

        private void Drain()
        {
            int n = 0; LogEventMessage m;
            while (n < MaxBatch && _queue.TryDequeue(out m))
            {
                Interlocked.Decrement(ref _pending); n++;
                string text = m.Message ?? "";
                int nl = text.IndexOf('\n');
                Rows.Add(new LogRow
                {
                    TimeText = m.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Level = m.Level,
                    Source = m.Title ?? "",
                    Message = nl < 0 ? text : text.Substring(0, nl).TrimEnd('\r') + " …",
                    Detail = m.Detail == null ? text : text + "\n" + m.Detail
                });
            }
            while (Rows.Count > Max) Rows.RemoveAt(0);
        }
    }
}

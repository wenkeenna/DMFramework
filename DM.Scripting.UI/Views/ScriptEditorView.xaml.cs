using DM.Core.Scripting;
using DM.Scripting.Services;
using DM.Scripting.UI.ViewModels;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace DM.Scripting.UI.Views
{
    /// <summary>
    /// ScriptEditorView.xaml 的交互逻辑
    /// </summary>
    public partial class ScriptEditorView : UserControl
    {
        private ScriptEditorViewModel _vm;
        private bool _sync;
        private CompletionWindow _win;

        public ScriptEditorView()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => Attach(e.NewValue as ScriptEditorViewModel);
            Editor.TextChanged += (s, e) => { if (_vm != null && !_sync) { _sync = true; _vm.Text = Editor.Text; _sync = false; } };
            Editor.TextArea.TextEntered += OnTextEntered;
        }

        private void Attach(ScriptEditorViewModel vm)
        {
            if (vm == null || ReferenceEquals(vm, _vm)) return;
            _vm = vm;

            // 命令名、选项值全部来自注册表，新增命令自动高亮
            using (var r = XmlReader.Create(new StringReader(ScriptHighlightingBuilder.BuildXshd(vm.Registry))))
                Editor.SyntaxHighlighting = HighlightingLoader.Load(r, HighlightingManager.Instance);

            vm.PropertyChanged += OnVmChanged;
            vm.InsertRequested += t => { Editor.TextArea.Selection.ReplaceSelectionWithText(t); Editor.Focus(); ShowCompletion(); };
            vm.JumpToLine += line =>
            {
                if (line < 1 || line > Editor.Document.LineCount) return;
                Editor.ScrollToLine(line);
                Editor.TextArea.Caret.Line = line;
                Editor.Focus();
            };
            SyncFromVm();
        }

        private void OnVmChanged(object s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScriptEditorViewModel.Text)) SyncFromVm();
            else if (e.PropertyName == nameof(ScriptEditorViewModel.ReadOnly)) Editor.IsReadOnly = _vm.ReadOnly;
        }

        private void SyncFromVm()
        {
            Editor.IsReadOnly = _vm.ReadOnly;
            if (_sync || Editor.Text == _vm.Text) return;
            _sync = true; Editor.Text = _vm.Text; _sync = false;          // 切换任务时整体替换
        }

        private void CmdList_DoubleClick(object s, MouseButtonEventArgs e)
        {
            var spec = CmdList.SelectedItem as ScriptCommandSpec;
            if (spec != null && _vm != null && !_vm.ReadOnly) _vm.InsertCommand.Execute(spec);
        }

        // ───── 补全 ─────
        private void OnTextEntered(object s, TextCompositionEventArgs e)
        {
            if (_vm == null || _vm.ReadOnly || string.IsNullOrEmpty(e.Text)) return;
            char c = e.Text[0];
            if (c == ' ') { if (_win != null) _win.Close(); ShowCompletion(); }
            else if (_win == null && (char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '.')) ShowCompletion();
        }

        private void ShowCompletion()
        {
            DocumentLine line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            string prefix = Editor.Document.GetText(line.Offset, Editor.CaretOffset - line.Offset);
            int replace;
            var items = _vm.Completion.GetItems(prefix, out replace);
            if (items.Count == 0) return;

            _win = new CompletionWindow(Editor.TextArea) { StartOffset = Editor.CaretOffset - replace };
            foreach (CompletionItem it in items) _win.CompletionList.CompletionData.Add(new Item(it));
            _win.Closed += (s, e) => _win = null;
            _win.Show();
        }

        private sealed class Item : ICompletionData
        {
            private readonly CompletionItem _i;
            public Item(CompletionItem i) { _i = i; }
            public ImageSource Image => null;
            public string Text => _i.Text;
            public object Content => _i.Text;
            public object Description => _i.Description;
            public double Priority => 0;
            public void Complete(TextArea ta, ISegment seg, EventArgs args) { ta.Document.Replace(seg, _i.Text); }
        }
    }
}

using DM.Core.Data;
using DM.Parameters.Services;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DM.Parameters.Models
{
    public enum ParamKind { Int, Double, Bool, String }

    public sealed class ScopeItem
    {
        public ScopeItem(DataScope scope, string name, bool editable) { Scope = scope; Name = name; Editable = editable; }
        public DataScope Scope { get; }
        public string Name { get; }
        public bool Editable { get; }
    }

    /// <summary>树节点基类：组节点（Root）和叶子节点（参数）。展开状态保存在节点上，刷新时据此恢复。</summary>
    public abstract class ParamNode : BindableBase
    {
        private bool _expanded, _selected, _visible = true;
        public abstract bool IsLeaf { get; }
        public string Name { get; protected set; }
        public bool IsExpanded { get { return _expanded; } set { SetProperty(ref _expanded, value); } }
        public bool IsSelected { get { return _selected; } set { SetProperty(ref _selected, value); } }
        /// <summary>搜索过滤：不匹配的节点隐藏。</summary>
        public bool IsVisible { get { return _visible; } set { SetProperty(ref _visible, value); } }
    }

    public sealed class ParamGroupNode : ParamNode
    {
        public ParamGroupNode(string root) { Name = root; }
        public override bool IsLeaf => false;
        public ObservableCollection<ParamLeafNode> Children { get; } = new ObservableCollection<ParamLeafNode>();
        public string CountText => "(" + Children.Count + ")";
        public void Rename(string root) { Name = root; RaisePropertyChanged(nameof(Name)); }
        public void RefreshCount() { RaisePropertyChanged(nameof(CountText)); }
    }

    public sealed class ParamLeafNode : ParamNode
    {
        private object _value;
        private string _display, _editText;
        private bool _editing;

        public ParamLeafNode(string root, string child, DataScope scope, object value)
        {
            Root = root; Name = child; Scope = scope;
            Kind = ParameterValues.InferKind(value);
            _value = value; _display = ParameterValues.Format(value); _editText = _display;
        }

        public override bool IsLeaf => true;
        public string Root { get; private set; }
        public string Key => Root + "." + Name;
        public DataScope Scope { get; }
        public ParamKind Kind { get; }
        public object Value => _value;
        public string Display { get { return _display; } private set { SetProperty(ref _display, value); } }
        public bool IsBool => Kind == ParamKind.Bool;
        public bool BoolValue => _value is bool && (bool)_value;

        /// <summary>行内编辑文本框绑定的内容；确认前不影响 Value。</summary>
        public string EditText { get { return _editText; } set { SetProperty(ref _editText, value ?? ""); } }
        public bool IsEditing { get { return _editing; } set { SetProperty(ref _editing, value); } }

        public void SetValue(object v)
        {
            _value = v; Display = ParameterValues.Format(v); EditText = Display;
            RaisePropertyChanged(nameof(BoolValue));
        }

        public void Move(string root, string child)
        {
            Root = root; Name = child;
            RaisePropertyChanged(nameof(Root)); RaisePropertyChanged(nameof(Name)); RaisePropertyChanged(nameof(Key));
        }
    }
}

using DM.Core.Data;
using DM.Parameters.Services;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
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

    public sealed class ParameterRow : BindableBase
    {
        private object _value;
        private string _display;

        public ParameterRow(string key, DataScope scope, object value)
        {
            Key = key;
            int i = key.IndexOf('.');
            Root = i > 0 ? key.Substring(0, i) : key;
            Child = i > 0 ? key.Substring(i + 1) : "";
            Scope = scope;
            Kind = ParameterValues.InferKind(value);
            _value = value;
            _display = ParameterValues.Format(value);
        }

        public string Key { get; }
        public string Root { get; }
        public string Child { get; }
        public DataScope Scope { get; }
        public ParamKind Kind { get; }
        public object Value => _value;
        public string Display { get { return _display; } private set { SetProperty(ref _display, value); } }

        public void SetValue(object v) { _value = v; Display = ParameterValues.Format(v); }
    }
}

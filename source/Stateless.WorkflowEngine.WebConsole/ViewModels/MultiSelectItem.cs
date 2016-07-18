using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.ViewModels
{
    public class MultiSelectItem
    {
        public MultiSelectItem()
        {
        }

        public MultiSelectItem(string value, string text, bool isSelected)
        {
            this.Value = value;
            this.Text = text;
            this.IsSelected = isSelected;
        }

        public string Value { get; set; }

        public string Text { get; set; }

        public bool IsSelected { get; set; }
    }
}

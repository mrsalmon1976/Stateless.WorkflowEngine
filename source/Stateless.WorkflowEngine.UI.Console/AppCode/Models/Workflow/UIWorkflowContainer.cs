using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.UI.Console.Models.Workflow
{
    [BsonIgnoreExtraElements] 
    public class UIWorkflowContainer : INotifyPropertyChanged
    {
        private bool _isSelected = false;

        public string WorkflowType { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public string WorkflowTypeName
        {
            get
            {
                if (String.IsNullOrWhiteSpace(this.WorkflowType))
                {
                    return String.Empty;
                }

                ParsedAssemblyQualifiedName.ParsedAssemblyQualifiedName p = new ParsedAssemblyQualifiedName.ParsedAssemblyQualifiedName(this.WorkflowType);
                return p.TypeName;

            }
        }

        public UIWorkflow Workflow { get; set; }

        public bool IsSelected 
        { 
            get 
            { 
                return _isSelected; 
            } 
            set
            { 
                _isSelected = value; 
                OnChanged("IsSelected"); 
            }
        }

        private void OnChanged(string prop)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }

    }
}

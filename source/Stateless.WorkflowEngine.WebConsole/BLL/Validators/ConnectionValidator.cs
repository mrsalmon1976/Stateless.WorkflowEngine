using Stateless.WorkflowEngine.WebConsole.BLL.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Validators
{
    public interface IConnectionValidator
    {
        ValidationResult Validate(ConnectionModel model);
    }

    public class ConnectionValidator : IConnectionValidator
    {
        public ValidationResult Validate(ConnectionModel model)
        {
            ValidationResult result = new ValidationResult();

            if (String.IsNullOrWhiteSpace(model.Host))
            {
                result.Messages.Add("Host cannot be empty");
            }
            if (!model.Port.HasValue || model.Port.Value <= 0)
            {
                result.Messages.Add("Port must have a numeric value");
            }
            if (String.IsNullOrWhiteSpace(model.ActiveCollection))
            {
                result.Messages.Add("Active collection cannot be empty");
            }
            if (String.IsNullOrWhiteSpace(model.CompletedCollection))
            {
                result.Messages.Add("Completed collection cannot be empty");
            }

            return result;
        }
    }
}

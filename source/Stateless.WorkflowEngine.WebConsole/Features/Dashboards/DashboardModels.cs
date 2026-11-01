using Stateless.WorkflowEngine.WebConsole.Storage.Models;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;

namespace Stateless.WorkflowEngine.WebConsole.Features.Dashboards
{
    public class DashboardRequest
    {
        public string? Name { get; set; }
    }

    public class DashboardResponse
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public bool IsDefault { get; set; }

        public int ConnectionCount { get; set; }

        public static DashboardResponse FromModel(DashboardModel model, int connectionCount)
        {
            return new DashboardResponse
            {
                Id = model.Id,
                Name = model.Name,
                IsDefault = model.IsDefault,
                ConnectionCount = connectionCount
            };
        }
    }

    public class DashboardValidator
    {
        public const int MaxNameLength = 100;

        private readonly IUserStore _userStore;

        public DashboardValidator(IUserStore userStore)
        {
            _userStore = userStore;
        }

        public virtual IReadOnlyList<string> Validate(DashboardRequest request, Guid? existingDashboardId)
        {
            var errors = new List<string>();

            if (String.IsNullOrWhiteSpace(request.Name))
            {
                errors.Add("Name cannot be empty");
            }
            else
            {
                string name = request.Name.Trim();
                if (name.Length > MaxNameLength)
                {
                    errors.Add($"Name cannot be longer than {MaxNameLength} characters");
                }
                if (_userStore.Dashboards.Any(x => x.Id != existingDashboardId && String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add("A dashboard with the supplied name already exists");
                }
            }

            return errors;
        }
    }
}

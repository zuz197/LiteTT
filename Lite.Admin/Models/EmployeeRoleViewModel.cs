using Lite.Models.HR;

namespace Lite.Admin.Models
{
    public class EmployeeRoleViewModel
    {
        public Employee Employee { get; set; } = new Employee();
        public List<string> SelectedRoles { get; set; } = new List<string>();
    }
}


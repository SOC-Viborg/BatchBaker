using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BatchBaker.Configuration
{
    public class ActiveDirectory
    {
        // Empty means "the domain this computer is joined to".
        public string Domain { get; set; } = string.Empty;
        public ADDepartments Departments { get; set; } = new();

        // Fills in the domain and turns each department's OU path into a full
        // distinguished name, so the same appsettings.json works in any domain with
        // the same OU layout. An OU path that already ends in DC=... is left as is.
        public void ResolveDomain()
        {
            if (string.IsNullOrWhiteSpace(Domain))
            {
                try
                {
                    Domain = System.DirectoryServices.ActiveDirectory.Domain.GetComputerDomain().Name;
                }
                catch (Exception)
                {
                    Domain = string.Empty; // not domain-joined; MainWindow warns about it at startup
                    return;
                }
            }

            string domainDn = string.Join(",", Domain.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(part => $"DC={part}"));
            foreach (PropertyInfo property in typeof(ADDepartments).GetProperties())
            {
                string ou = (property.GetValue(Departments) as string ?? string.Empty).Trim().TrimEnd(',');
                if (ou.Length > 0 && !ou.Contains("DC=", StringComparison.OrdinalIgnoreCase))
                {
                    property.SetValue(Departments, $"{ou},{domainDn}");
                }
            }
        }
    }
    public class ADDepartments
    {
        [CSVValue(ColumnName = "Programmør")]
        public string Data_Programmers { get; set; } = default!;

        [CSVValue(ColumnName = "Infrastruktur")]
        public string Data_Infrastructure { get; set; } = default!;

        [CSVValue(ColumnName = "IT Support")]
        public string Data_ITSupport { get; set; } = default!;

        [CSVValue(ColumnName = "Cyber Security")]
        public string Data_CyberSecurity { get; set; } = default!;

        [CSVValue(ColumnName = "Administration")]
        public string Business_Administration { get; set; } = default!;

        [CSVValue(ColumnName = "Handel")]
        public string Business_Marketing { get; set; } = default!;

        [CSVValue(ColumnName = "Økonomi")]
        public string Business_Economy { get; set; } = default!;

        [CSVValue(ColumnName = "Automatik")]
        public string Energy_Automation { get; set; } = default!;

        [CSVValue(ColumnName = "Elektronik")]
        public string Energy_Electronics { get; set; } = default!;

        [CSVValue(ColumnName = "Elektriker")]
        public string Energy_Electricians { get; set; } = default!;
    }
    public class CSVValueAttribute : Attribute
    {
        public string ColumnName { get; set; } = default!;
    }
}

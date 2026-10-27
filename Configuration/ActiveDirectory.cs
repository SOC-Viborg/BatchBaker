using System;
using System.Collections.Generic;
using System.Text;

namespace BatchBaker.Configuration
{
    public class ActiveDirectory
    {
        public string Domain { get; set; } = default!;
        public ADDepartments Departments { get; set; } = default!;
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

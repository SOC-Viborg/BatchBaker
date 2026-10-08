using BatchBaker.Configuration;
using Microsoft.Extensions.Options;
using System.DirectoryServices;
using System.Reflection;

namespace BatchBaker
{
    public class ADService
    {
        public ADDepartments departments { get; set; } = default!;
        public ADService(IOptions<ActiveDirectory> activeDirectoryOptions)
        {
            departments = activeDirectoryOptions.Value.Departments;
        }

        public bool DoesOuExist(string distinguishedName)
        {
#if DEBUG
            return true; // Always return true in debug mode for testing purposes
#else 
            // Eksempel: "OU=Salg,DC=firma,DC=local"
            string path = $"LDAP://{distinguishedName}";

            try
            {
                // Windows logger automatisk ind med applikationens egne rettigheder
                using (var entry = new DirectoryEntry(path))
                {
                    // Tvinger AD til at tjekke om stien findes i virkeligheden
                    string schema = entry.SchemaClassName;
                    return schema.Equals("organizationalUnit", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false; // OU findes ikke, eller applikationen har ikke rettigheder til at læse den
            }
#endif
        }

        public bool TryValidateDepartment(string name, out string Ou)
        {
            PropertyInfo[] properties = typeof(ADDepartments).GetProperties();
            foreach (PropertyInfo property in properties) 
            { 
                var formattedName = name
                    .Replace('_', ' ')
                    .Replace('-', ' ')
                    .Trim();

                var att = property.GetCustomAttribute<CSVValueAttribute>();
                if (att != null && att.ColumnName.Equals(formattedName, StringComparison.CurrentCultureIgnoreCase))
                {
                    Ou = property.GetValue(departments)?.ToString() ?? "null";
                    return true;
                }
            }
            Ou = default!;
            return false; 
        }
    }   
}

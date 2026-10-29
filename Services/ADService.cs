using BatchBaker.Configuration;
using BatchBaker.Models;
using Microsoft.Extensions.Options;
using System.DirectoryServices;
using System.Reflection;

namespace BatchBaker.Services
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

        // The department names a CSV (or the editor's dropdown) may use, in config order.
        public List<string> DepartmentNames => typeof(ADDepartments).GetProperties()
            .Select(p => p.GetCustomAttribute<CSVValueAttribute>()?.ColumnName)
            .OfType<string>()
            .ToList();

        // Each department with the sector it belongs to and its OU, in config order.
        // The sector is the OU directly above the department's own OU
        // (OU=IT-Support,OU=Data,... gives "Data").
        public List<(string Sector, string Department, string Ou)> DepartmentLayout => typeof(ADDepartments).GetProperties()
            .Select(p => (Property: p, Name: p.GetCustomAttribute<CSVValueAttribute>()?.ColumnName))
            .Where(x => x.Name != null)
            .Select(x =>
            {
                string ou = x.Property.GetValue(departments)?.ToString() ?? string.Empty;
                var ouParts = ou.Split(',')
                    .Select(part => part.Trim())
                    .Where(part => part.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
                    .Select(part => part.Substring(3))
                    .ToList();
                string sector = ouParts.Count >= 2 ? ouParts[1] : x.Property.Name.Split('_')[0];
                return (sector, x.Name!, ou);
            })
            .ToList();

        // Reads the user accounts in every department OU. An OU that can't be read
        // (missing, no access, no domain) is reported in FailedOus instead of
        // stopping the rest.
        public (List<UserOverview> Users, List<string> FailedOus) GetDirectoryUsers()
        {
            var users = new List<UserOverview>();
            var failedOus = new List<string>();

            foreach (var (_, department, ou) in DepartmentLayout)
            {
                try
                {
                    using var entry = new DirectoryEntry($"LDAP://{ou}");
                    using var searcher = new DirectorySearcher(entry, "(&(objectCategory=person)(objectClass=user))")
                    {
                        SearchScope = SearchScope.Subtree,
                        PageSize = 500
                    };
                    searcher.PropertiesToLoad.AddRange(new[] { "givenName", "sn", "sAMAccountName", "mail", "co", "telephoneNumber", "userAccountControl", "whenCreated" });

                    using var results = searcher.FindAll();
                    foreach (SearchResult result in results)
                    {
                        string Get(string name) => result.Properties[name].Count > 0 ? result.Properties[name][0]?.ToString() ?? string.Empty : string.Empty;

                        // Bit 2 of userAccountControl means the account is disabled.
                        bool disabled = int.TryParse(Get("userAccountControl"), out int flags) && (flags & 2) != 0;
                        string created = result.Properties["whenCreated"].Count > 0 && result.Properties["whenCreated"][0] is DateTime when
                            ? when.ToLocalTime().ToString("dd-MM-yyyy HH:mm")
                            : string.Empty;

                        users.Add(new UserOverview
                        {
                            Status = disabled ? "Deaktiveret" : "Aktiv",
                            FirstName = Get("givenName"),
                            LastName = Get("sn"),
                            Username = Get("sAMAccountName"),
                            Email = Get("mail"),
                            Department = department,
                            Country = Get("co"),
                            PhoneNumber = Get("telephoneNumber"),
                            Created = created
                        });
                    }
                }
                catch (Exception)
                {
                    failedOus.Add(ou);
                }
            }

            return (users, failedOus);
        }

        // Returns the official spelling of a department name, or null if it matches none.
        public string? MatchDepartment(string name)
        {
            string formattedName = NormalizeDepartment(name);
            return DepartmentNames.FirstOrDefault(d => d.Equals(formattedName, StringComparison.CurrentCultureIgnoreCase));
        }

        private static string NormalizeDepartment(string name)
            => (name ?? string.Empty).Replace('_', ' ').Replace('-', ' ').Trim();

        public bool TryValidateDepartment(string name, out string Ou)
        {
            PropertyInfo[] properties = typeof(ADDepartments).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                var formattedName = NormalizeDepartment(name);

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

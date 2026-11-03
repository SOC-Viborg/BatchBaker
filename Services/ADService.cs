using BatchBaker.Configuration;
using BatchBaker.Models;
using Microsoft.Extensions.Options;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Reflection;

namespace BatchBaker.Services
{
    public class ADService
    {
        public enum OuStatus { Found, Missing, Unreadable }

        public ADDepartments departments { get; set; } = default!;
        private readonly ActiveDirectory settings;

        // The Domain Controller (or, if none are listed in the settings, the domain
        // name) that BatchBaker talks to.
        public string Server { get; private set; }

        public ADService(IOptions<ActiveDirectory> activeDirectoryOptions)
        {
            settings = activeDirectoryOptions.Value;
            departments = settings.Departments;
            Server = ConfiguredServers().FirstOrDefault() ?? string.Empty;
        }

        private List<string> ConfiguredServers()
        {
            var controllers = settings.DomainControllers.Where(dc => !string.IsNullOrWhiteSpace(dc)).Select(dc => dc.Trim()).ToList();
            if (controllers.Count > 0)
            {
                return controllers;
            }
            return string.IsNullOrWhiteSpace(settings.Domain) ? new List<string>() : new List<string> { settings.Domain };
        }

        private string LdapPath(string distinguishedName)
            => string.IsNullOrEmpty(Server) ? $"LDAP://{distinguishedName}" : $"LDAP://{Server}/{distinguishedName}";

        // Tries the listed Domain Controllers in order and uses the first that answers.
        // Returns null on success, or a message saying why none could be reached.
        public string? ConnectToDomainController()
        {
            var servers = ConfiguredServers();
            if (servers.Count == 0)
            {
                return "Der er intet domæne eller nogen domænecontroller at forbinde til. Angiv dem i appsettings.json.";
            }

            var failures = new List<string>();
            foreach (string server in servers)
            {
                try
                {
                    using var rootDse = new DirectoryEntry($"LDAP://{server}/RootDSE");
                    _ = rootDse.NativeObject; // forces the connection
                    Server = server;
                    return null;
                }
                catch (Exception ex)
                {
                    failures.Add($"{server}: {DescribeError(ex)}");
                }
            }
            return $"Der kunne ikke oprettes forbindelse til Active Directory.{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, failures)}";
        }

        public OuStatus CheckOu(string distinguishedName)
        {
#if DEBUG
            return OuStatus.Found; // Always found in debug mode for testing purposes
#else
            try
            {
                // Windows logger automatisk ind med applikationens egne rettigheder
                using var entry = new DirectoryEntry(LdapPath(distinguishedName));
                // Tvinger AD til at tjekke om stien findes i virkeligheden
                string schema = entry.SchemaClassName;
                return schema.Equals("organizationalUnit", StringComparison.OrdinalIgnoreCase) ? OuStatus.Found : OuStatus.Missing;
            }
            catch (Exception ex)
            {
                return HasHResult(ex, ObjectNotFound) ? OuStatus.Missing : OuStatus.Unreadable;
            }
#endif
        }

        // The distinguished name of an account already using this logon name or UPN
        // anywhere in the domain, or null if both are free.
        public string? FindExistingUser(string username, string userPrincipalName)
        {
            using var context = new PrincipalContext(ContextType.Domain, Server);
            using var bySamAccountName = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, username);
            if (bySamAccountName != null)
            {
                return bySamAccountName.DistinguishedName;
            }
            using var byUpn = UserPrincipal.FindByIdentity(context, IdentityType.UserPrincipalName, userPrincipalName);
            return byUpn?.DistinguishedName;
        }

        private const int AccessDenied = unchecked((int)0x80070005);
        private const int ServerNotOperational = unchecked((int)0x8007203A);
        private const int ObjectNotFound = unchecked((int)0x80072030);

        private static bool HasHResult(Exception ex, int hResult)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e.HResult == hResult)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasException<T>(Exception ex) where T : Exception
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is T)
                {
                    return true;
                }
            }
            return false;
        }

        // A clear Danish explanation of the common AD failures. Anything else falls back
        // to AD's own text. The codes are checked rather than the message text, which
        // depends on the language of Windows.
        public static string DescribeError(Exception ex)
        {
            string details = ErrorDetails(ex);

            if (HasException<PrincipalServerDownException>(ex) || HasHResult(ex, ServerNotOperational))
            {
                return "Der kan ikke oprettes forbindelse til domænecontrolleren. Tjek netværket og at serveren kører.";
            }
            if (HasException<UnauthorizedAccessException>(ex) || HasHResult(ex, AccessDenied) || details.Contains("INSUFF_ACCESS_RIGHTS"))
            {
                return "Manglende rettigheder: din Windows-konto må ikke udføre dette i Active Directory.";
            }
            if (details.Contains("000021C8"))
            {
                return "Logonnavnet (brugernavn@domæne) er allerede i brug af en anden konto i domænet.";
            }
            if (HasException<PrincipalExistsException>(ex))
            {
                return "Brugeren findes allerede i Active Directory.";
            }
            if (HasException<PasswordException>(ex) || details.Contains("0000052D"))
            {
                return "Adgangskoden opfylder ikke domænets adgangskodepolitik.";
            }
            return details;
        }

        // The full technical error, for the log. AD's specific reason (e.g. which
        // attribute it rejected) is only in ExtendedErrorMessage.
        public static string ErrorDetails(Exception ex)
        {
            string details = $"{ex.GetType().Name}: {ex.Message.Trim()}";
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e != ex && e.Message.Trim() != ex.Message.Trim())
                {
                    details += $" ({e.Message.Trim()})";
                }
                if (e is DirectoryServicesCOMException com && !string.IsNullOrWhiteSpace(com.ExtendedErrorMessage))
                {
                    details += $" [{com.ExtendedErrorMessage.Trim().TrimEnd('\0')}]";
                }
            }
            return details;
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
            ConnectToDomainController(); // picks a working server; an unreachable one shows up in FailedOus

            var users = new List<UserOverview>();
            var failedOus = new List<string>();

            foreach (var (_, department, ou) in DepartmentLayout)
            {
                try
                {
                    using var entry = new DirectoryEntry(LdapPath(ou));
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

using System.IO;
using System.Text.Json;

namespace BatchBaker
{
    // Keeps the LDAP path out of source so it doesn't need a recompile per domain.
    public class AppSettings
    {
        public ActiveDirectorySettings ActiveDirectory { get; set; } = new();

        private static AppSettings? _instance;
        public static AppSettings Instance => _instance ??= Load();

        private static AppSettings Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
        }
    }

    public class ActiveDirectorySettings
    {
        public string LdapPath { get; set; } = "LDAP://CN=Users,DC=yourdomain,DC=com";
    }
}

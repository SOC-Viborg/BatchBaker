using BatchBaker.Models;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BatchBaker
{
    internal static class MainWindowHelpers
    {
        public static string GenerateNewPassword(int length = 12)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Because the users are recieving a new password, we generate a PDF for them to print when they log on for the first time with their new login.
        // Returns the path of the written file.
        public static string GeneratePDF(People person, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            string pdfFilePath = Path.Combine(outputDirectory, $"{person.Username}_credentials.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(20));
                    page.Header()
                        .Text("Your New Account Credentials")
                        .SemiBold().FontSize(36).FontColor(Colors.Blue.Medium);
                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Spacing(20);
                            x.Item().Text($"Username: {person.Username}");
                            x.Item().Text($"Temporary Password: {person.TemporaryPassword}");
                            x.Item().Text("Please change your password upon first login.");
                        });
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            })
            .GeneratePdf(pdfFilePath);

            return pdfFilePath;
        }

        public static IEnumerable<People> ReadCSV(string filePath)
        {
            try
            {
                // Use the provided file path directly (already has .csv extension)
                string fullPath = filePath.EndsWith(".csv") ? filePath : Path.ChangeExtension(filePath, ".csv");

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"CSV file not found: {fullPath}");
                }

                // Read file with UTF-8 encoding to properly handle special characters (Æ, Ø, Å)
                string[] lines = File.ReadAllLines(fullPath, Encoding.UTF8);

                // Skip header row and project each line as a Person
                return lines.Skip(1).Select(line =>
                {
                    string[] data = line.Split(',');
                    if (data.Length < 9)
                    {
                        throw new FormatException($"CSV line does not contain expected 9 fields: {line}");
                    }

                    // Because most users are from Denmark, the standard for country is to default to Denmark if the field is empty.

                    if (string.IsNullOrWhiteSpace(data[5]))
                    {
                        data[5] = "Denmark";
                    }

                    //// Parse phone number, trimming whitespace and removing formatting characters
                    //string phoneStr = data[7].Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
                    //if (!int.TryParse(phoneStr, out int phoneNumber))
                    //{
                    //    // If it's not parseable as int, store 0 or try alternative approaches
                    //    phoneNumber = 0;
                    //}

                    // Use the full constructor with all 9 fields, trimming whitespace from all fields
                    //return new People(
                    //    data[0].Trim(), 
                    //    data[1].Trim(), 
                    //    data[2].Trim(), 
                    //    data[3].Trim(), 
                    //    data[4].Trim(), 
                    //    data[5].Trim(), 
                    //    data[6].Trim(), 
                    //    phoneNumber, 
                    //    data[8].Trim()
                    //);
                    return new People
                    {
                        FirstName = data[0].Trim(),
                        LastName = data[1].Trim(),
                        Username = data[2].Trim(),
                        Email = data[3].Trim(),
                        Department = data[4].Trim(),
                        Country = data[5].Trim(),
                        // data[6] is the CSV's Titel column, which is no longer used.
                        PhoneNumber = data[7].Trim(),
                        TemporaryPassword = data[8].Trim()
                    };
                }).ToList(); // Materialize the list to ensure all parsing happens before returning
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"Error parsing CSV file: {ex.Message}");
            }
        }

        // Flags rows whose Username already exists in the database, or is repeated
        // more than once within the same file, so duplicates never reach Save.
        // Sets ImportSuccess/ImportError on each row in place.
        public static void ValidateForDuplicateUsernames(IEnumerable<People> people, IEnumerable<string> existingUsernames)
        {
            var existing = new HashSet<string>(existingUsernames, StringComparer.OrdinalIgnoreCase);
            var list = people.ToList();

            var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var person in list)
            {
                string username = person.Username ?? string.Empty;
                occurrences[username] = occurrences.GetValueOrDefault(username) + 1;
            }

            foreach (var person in list)
            {
                string username = person.Username ?? string.Empty;
                if (existing.Contains(username))
                {
                    person.ImportSuccess = false;
                    person.ImportError = "Username already exists in the database";
                }
                else if (occurrences[username] > 1)
                {
                    person.ImportSuccess = false;
                    person.ImportError = "Duplicate username within this file";
                }
                else
                {
                    person.ImportSuccess = true;
                    person.ImportError = string.Empty;
                }
            }
        }

        // Danish display labels for UI
        public static readonly Dictionary<string, string> DanishLabels = new()
        {
            { nameof(People.FirstName), "Fornavn" },
            { nameof(People.LastName), "Efternavn" },
            { nameof(People.Username), "Brugernavn" },
            { nameof(People.Email), "Email" },
            { nameof(People.Department), "Afdeling" },
            { nameof(People.Country), "Land" },
            { nameof(People.PhoneNumber), "Telefonnummer" },
            { nameof(People.TemporaryPassword), "Midlertidig adgangskode" }
        };

        // Creates one user in AD, or marks the row as a pre-existing duplicate instead
        // of silently doing nothing. Throws on genuine failures (connection, policy,
        // permissions) — callers should catch per-row so one bad row doesn't abort a batch.
        public static void CreateActiveDirectoryUser(People person, string ldapPath)
        {
            string username = person.Username ?? string.Empty;

            using var entry = new DirectoryEntry(ldapPath);
            using var search = new DirectorySearcher(entry)
            {
                Filter = $"(&(objectClass=user)(sAMAccountName={EscapeLdapFilterValue(username)}))"
            };

            SearchResult? existingResult = search.FindOne();
            if (existingResult != null)
            {
                person.ImportSuccess = false;
                person.ImportError = "User already exists in Active Directory";
                return;
            }

            DirectoryEntry newUser = entry.Children.Add($"CN={username}", "user");
            try
            {
                newUser.Properties["sAMAccountName"].Value = username;
                newUser.Properties["userPrincipalName"].Value = person.Email ?? string.Empty;
                newUser.Properties["givenName"].Value = person.FirstName ?? string.Empty;
                newUser.Properties["sn"].Value = person.LastName ?? string.Empty;
                newUser.Properties["displayName"].Value = $"{person.FirstName} {person.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(person.Department))
                {
                    newUser.Properties["department"].Value = person.Department;
                }
                if (!string.IsNullOrWhiteSpace(person.Country))
                {
                    newUser.Properties["co"].Value = person.Country;
                }
                if (!string.IsNullOrWhiteSpace(person.PhoneNumber))
                {
                    newUser.Properties["telephoneNumber"].Value = person.PhoneNumber;
                }
                newUser.CommitChanges();

                newUser.Invoke("SetPassword", new object[] { person.TemporaryPassword ?? string.Empty });
                newUser.Properties["pwdLastSet"].Value = 0; // forces a password change at next logon
                newUser.Properties["userAccountControl"].Value = 512; // normal, enabled account
                newUser.CommitChanges();

                person.ImportSuccess = true;
                person.ImportError = string.Empty;
            }
            finally
            {
                newUser.Dispose();
            }
        }

        // Creates every row in Active Directory, catching each row's failure
        // individually so one bad row doesn't abort the rest of the batch.
        // Results land in ImportSuccess/ImportError per row, same as import validation.
        public static void CreateActiveDirectoryUsers(IEnumerable<People> people, string ldapPath)
        {
            foreach (var person in people)
            {
                try
                {
                    CreateActiveDirectoryUser(person, ldapPath);
                }
                catch (Exception ex)
                {
                    person.ImportSuccess = false;
                    person.ImportError = ex.Message;
                }
            }
        }

        // Escapes RFC 4515 special characters so a username can't alter the meaning
        // of the LDAP search filter it's inserted into.
        private static string EscapeLdapFilterValue(string value)
        {
            return value
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00");
        }
    }
}

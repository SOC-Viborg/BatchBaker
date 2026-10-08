using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using BatchBaker.Configuration;
using BatchBaker.Data;
using BatchBaker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using System.DirectoryServices.AccountManagement;

namespace BatchBaker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public AppDbContext dbContext = new AppDbContext();
        private readonly ActiveDirectory activeDirectory;
        private readonly ADService adService;
        public MainWindow(IOptions<ActiveDirectory> activeDirectoryOptions, ADService adService)
        {
            InitializeComponent();
            activeDirectory = activeDirectoryOptions.Value;
            this.adService = adService;

            PropertyInfo[] properties = typeof(ADDepartments).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                string propertyName = property.Name;
                string propertyValue = property.GetValue(activeDirectory.Departments)?.ToString() ?? "null";
                Console.WriteLine($"{propertyName}: {propertyValue}");
                
                var exists = adService.DoesOuExist(propertyValue);
                if (!exists)
                {
                    MessageBox.Show("The OU does not exist in Active Directory: " + propertyValue, "OU Existence Check", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // MessageBox.Show($"OU '{propertyName}' with DN '{propertyValue}' exists: {exists}", "OU Existence Check", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            //var programmersOuexists = adService.DoesOuExist(departments.Data_Programmers);
            //var infrastructureOuexists = adService.DoesOuExist(departments.Data_Infrastructure);
            //var itSupportOuexists = adService.DoesOuExist(departments.Data_ITSupport);



            //var test = configuration["ActiveDirectory:Departments:Data_Programmers"];
            //MessageBox.Show(departments.Business_Marketing);
            //try
            //{
            //    var repository = new PersonRepository();
            //    repository.EnsureCreated();
            //    ListViewPeople.ItemsSource = repository.LoadAll();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"Error loading saved users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //}
            DatabaseInitializer.EnsureSchema(dbContext);
            LogAction("Application Started", string.Empty);
            var import = dbContext.ImportSets.Include(i => i.People).OrderByDescending(i => i.ImportDate).FirstOrDefault();
            if (import != null)
            {
                ListViewPeople.ItemsSource = import.People;
            }
        }

        // Records a timestamped entry for the activity log, viewable from Help > Logs.
        private void LogAction(string action, string details)
        {
            dbContext.ActivityLogs.Add(new ActivityLog
            {
                Timestamp = DateTime.Now,
                Action = action,
                Details = details
            });
            dbContext.SaveChanges();
        }

        private void OnViewLogsClicked(object sender, RoutedEventArgs e)
        {
            var logs = dbContext.ActivityLogs.OrderByDescending(l => l.Timestamp).ToList();
            var logViewer = new LogViewerWindow(logs) { Owner = this };
            logViewer.ShowDialog();
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
        // Validation before attempting a save.
            var people = (ListViewPeople.ItemsSource as IEnumerable<People>)?.ToList();
            if (people == null || people.Count == 0)
            {
                MessageBox.Show("No records loaded to save. Import a CSV file first.", "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var existingUsernames = dbContext.People.Select(p => p.Username).ToList();
            MainWindowHelpers.ValidateForDuplicateUsernames(people, existingUsernames); 

            var validPeople = people.Where(p => p.ImportSuccess).ToList();
            int skipped = people.Count - validPeople.Count;

            if (validPeople.Count == 0)
            {
                MessageBox.Show("Nothing was saved — every record's username already exists in the database.", "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogAction("Save", $"{validPeople.Count} valid record(s), {skipped} skipped as duplicate(s).");

            var SavetoAD = MessageBox.Show("Do you want to save the valid records to Active Directory as well?", "Save to Active Directory", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (SavetoAD == MessageBoxResult.Yes)
            {
                SaveUsersToActiveDirectory(validPeople);
            }
        }

        // Creates each person in the OU matching their department. Every row is
        // handled on its own so one failure doesn't stop the rest, and each outcome
        // is written to the activity log and the grid's Status/Reason columns.
        private void SaveUsersToActiveDirectory(List<People> people)
        {
            LogAction("AD Save Started", $"{people.Count} user(s), domain '{activeDirectory.Domain}'.");

            foreach (var person in people)
            {
                if (!adService.TryValidateDepartment(person.Department, out string ou))
                {
                    person.ImportSuccess = false;
                    person.ImportError = $"No OU matches department '{person.Department}'";
                    LogAction("AD User Skipped", $"{person.Username}: {person.ImportError}.");
                    continue;
                }

                try
                {
                    using var context = new PrincipalContext(ContextType.Domain, activeDirectory.Domain, ou);
                    using var user = new UserPrincipal(context)
                    {
                        Name = GetObjectName(context, person),
                        SamAccountName = person.Username,
                        GivenName = NullIfBlank(person.FirstName),
                        Surname = NullIfBlank(person.LastName),
                        DisplayName = NullIfBlank($"{person.FirstName} {person.LastName}".Trim()),
                        EmailAddress = NullIfBlank(person.Email),
                        VoiceTelephoneNumber = NullIfBlank(person.PhoneNumber),
                        UserPrincipalName = $"{person.Username}@{activeDirectory.Domain}",
                        Enabled = true
                    };
                    user.SetPassword(person.TemporaryPassword);
                    user.Save();
                    user.ExpirePasswordNow(); // forces a password change at first logon

                    person.ImportSuccess = true;
                    person.ImportError = string.Empty;
                    LogAction("AD User Created", $"{person.Username} in {ou}.");

                    // The account exists at this point, so a failure here is reported
                    // separately rather than marking the whole user as failed.
                    try
                    {
                        SetExtraAttributes(user, person);
                    }
                    catch (Exception ex)
                    {
                        person.ImportError = $"Created, but department/country not set: {ex.Message}";
                        LogAction("AD Attributes Failed", $"{person.Username}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    string reason = ex.InnerException == null
                        ? $"{ex.GetType().Name}: {ex.Message}"
                        : $"{ex.GetType().Name}: {ex.Message} ({ex.InnerException.Message})";
                    person.ImportSuccess = false;
                    person.ImportError = reason;
                    LogAction("AD User Failed", $"{person.Username} in {ou}: {reason}");
                }
            }

            int created = people.Count(p => p.ImportSuccess);
            int failed = people.Count - created;
            LogAction("AD Save Finished", $"{created} created, {failed} failed or skipped.");

            ListViewPeople.Items.Refresh();
            string summary = failed == 0
                ? $"Created {created} user(s) in Active Directory."
                : $"Created {created} user(s). {failed} failed or were skipped. See the Reason column, or Help > Logs for details.";
            MessageBox.Show(summary, "Active Directory", MessageBoxButton.OK, failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        // UserPrincipal has no properties for these, so they're written through the
        // underlying directory entry (which the principal owns, so it isn't disposed here).
        private static void SetExtraAttributes(UserPrincipal user, People person)
        {
            var entry = (System.DirectoryServices.DirectoryEntry)user.GetUnderlyingObject();
            bool changed = false;

            void SetIfPresent(string attribute, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    entry.Properties[attribute].Value = value;
                    changed = true;
                }
            }

            SetIfPresent("department", person.Department);
            SetIfPresent("co", person.Country);

            if (changed)
            {
                entry.CommitChanges();
            }
        }

        // The object name (CN) is what ADUC shows in its Name column and must be unique
        // within the OU, so a full name that's already taken there gets the username
        // appended. Falls back to the username if the person has no name at all.
        private static string GetObjectName(PrincipalContext context, People person)
        {
            string fullName = $"{person.FirstName} {person.LastName}".Trim();
            if (string.IsNullOrEmpty(fullName))
            {
                return person.Username;
            }

            using var existing = Principal.FindByIdentity(context, IdentityType.Name, fullName);
            return existing == null ? fullName : $"{fullName} ({person.Username})";
        }

        // AD rejects empty strings for these attributes; null leaves them unset.
        private static string? NullIfBlank(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value;


        // private void OnSaveClicked(object sender, RoutedEventArgs e)
        // {
        //     var people = (ListViewPeople.ItemsSource as IEnumerable<People>)?.ToList();
        //     if (people == null || people.Count == 0)
        //     {
        //         MessageBox.Show("No records loaded to save. Import a CSV file first.", "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Warning);
        //         return;
        //     }

        //     // Re-checked here (not just at Import) because the grid can also be
        //     // pre-populated from the last saved import set on startup, so a stale
        //     // batch shouldn't be able to re-save itself as duplicates.
        //     var existingUsernames = dbContext.People.Select(p => p.Username).ToList();
        //     MainWindowHelpers.ValidateForDuplicateUsernames(people, existingUsernames);

        //     var validPeople = people.Where(p => p.ImportSuccess).ToList();
        //     int skipped = people.Count - validPeople.Count;

        //     if (validPeople.Count == 0)
        //     {
        //         MessageBox.Show("Nothing was saved — every record's username already exists in the database.", "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Warning);
        //         return;
        //     }

        //     try
        //     {
        //         var import = new ImportSet { ImportDate = DateTime.Now, RecordCount = validPeople.Count };
        //         dbContext.ImportSets.Add(import);
        //         dbContext.SaveChanges();
        //         import.People.AddRange(validPeople);
        //         dbContext.SaveChanges();

        //         string message = skipped > 0
        //             ? $"Saved {validPeople.Count} record(s). Skipped {skipped} duplicate(s)."
        //             : $"Successfully saved {validPeople.Count} record(s) to the database.";
        //         MessageBox.Show(message, "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        //     }
        //     catch (Exception ex)
        //     {
        //         MessageBox.Show($"Error saving to database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //     }
        // }

        // private void OnCreateActiveDirectoryClicked(object sender, RoutedEventArgs e)
        // {
        //     var people = (ListViewPeople.ItemsSource as IEnumerable<People>)?.ToList();
        //     if (people == null || people.Count == 0)
        //     {
        //         MessageBox.Show("No records loaded. Import a CSV file first.", "Nothing to Create", MessageBoxButton.OK, MessageBoxImage.Warning);
        //         return;
        //     }

        //     string ldapPath = AppSettings.Instance.ActiveDirectory.LdapPath;
        //     MainWindowHelpers.CreateActiveDirectoryUsers(people, ldapPath);

        //     int succeeded = people.Count(p => p.ImportSuccess);
        //     int failed = people.Count - succeeded;
        //     string summary = failed == 0
        //         ? $"Created {succeeded} user(s) in Active Directory."
        //         : $"Created {succeeded} user(s). {failed} failed — see the Reason column for details.";
        //     MessageBox.Show(summary, "Active Directory", MessageBoxButton.OK, MessageBoxImage.Information);

        //     ListViewPeople.Items.Refresh();
        // }

        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ImportCSV_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = openFileDialog.FileName;
                    var people = MainWindowHelpers.ReadCSV(filePath).ToList();
                    var existingUsernames = dbContext.People.Select(p => p.Username).ToList();
                    MainWindowHelpers.ValidateForDuplicateUsernames(people, existingUsernames);

                    //foreach (var person in people)
                    //{
                    //    if (adService.TryValidateDepartment(person.Department, out string ou))
                    //    {
                    //        MessageBox.Show($"Department '{person.Department}' is valid and maps to OU '{ou}'.", "Department Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                    //    }
                    //    else
                    //    {
                    //        MessageBox.Show($"Department '{person.Department}' is invalid or does not map to a known OU.", "Department Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    //    }
                    //}

                    var previewWindow = new ImportPreviewWindow(people) { Owner = this };
                    if (previewWindow.ShowDialog() == true)
                    {
                        ListViewPeople.ItemsSource = previewWindow.ValidPeople;
                        int skipped = people.Count - previewWindow.ValidPeople.Count;
                        string message = skipped > 0
                            ? $"Imported {previewWindow.ValidPeople.Count} record(s). Skipped {skipped} duplicate(s)."
                            : $"Successfully imported {previewWindow.ValidPeople.Count} record(s).";
                        MessageBox.Show(message, "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        LogAction("Import", $"{System.IO.Path.GetFileName(filePath)}: {previewWindow.ValidPeople.Count} record(s) imported, {skipped} skipped.");
                    }
                    else
                    {
                        LogAction("Import Cancelled", System.IO.Path.GetFileName(filePath));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading CSV file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogAction("Import Failed", ex.Message);
                }
            }
        }

    }
}
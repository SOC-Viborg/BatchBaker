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
using BatchBaker.Services;

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
        private readonly PDFService pdfService;

        // The users being imported and edited. Gem always works on this list,
        // whichever view is shown.
        private List<People> batchPeople = new();

        private enum UserView { Batch, Saved, Directory }
        private UserView currentView = UserView.Batch;

        // Users read from Active Directory, kept until Opdatér or the next AD run.
        private List<UserOverview>? directoryUsers;
        private List<string> directoryFailedOus = new();

        // Tree node keys: everything, a sector, a department, or unmatched departments.
        private const string AllNode = "*";
        private const string UnknownNode = "?";
        private string selectedNode = AllNode;

        private bool ready;          // false while the window is still being built
        private bool buildingTree;   // ignores selection events while the tree is rebuilt

        public MainWindow(IOptions<ActiveDirectory> activeDirectoryOptions, ADService adService, PDFService pdfService)
        {
            InitializeComponent();
            activeDirectory = activeDirectoryOptions.Value;
            this.adService = adService;
            this.pdfService = pdfService;

            CheckDomainAndOus(reportSuccess: false);

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
            LogAction("Program startet", string.Empty);
            var import = dbContext.ImportSets.Include(i => i.People).OrderByDescending(i => i.ImportDate).FirstOrDefault();
            if (import != null)
            {
                batchPeople = import.People;
            }

            ready = true;
            ViewSelector.SelectedIndex = 0; // shows the batch
        }

        // Warns if the computer isn't in a domain or a department OU can't be found,
        // in one message rather than one box per OU. At startup only problems are
        // reported; from the Administrér menu a clean result is confirmed too.
        private void CheckDomainAndOus(bool reportSuccess)
        {
            if (string.IsNullOrEmpty(activeDirectory.Domain))
            {
                MessageBox.Show("Computeren er ikke medlem af et domæne, så brugere kan ikke oprettes i Active Directory. Angiv domænet under ActiveDirectory:Domain i appsettings.json, hvis det ikke findes automatisk.",
                    "Kontrol af domæne", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var missingOus = adService.DepartmentLayout
                .Select(d => d.Ou)
                .Where(ou => !adService.DoesOuExist(ou))
                .ToList();
            if (missingOus.Count > 0)
            {
                MessageBox.Show($"Disse OU'er findes ikke i domænet '{activeDirectory.Domain}':{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, missingOus)}",
                    "Kontrol af OU", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (reportSuccess)
            {
                string note = string.Empty;
#if DEBUG
                note = $"{Environment.NewLine}{Environment.NewLine}(Debug-build: OU'erne tjekkes ikke rigtigt.)";
#endif
                MessageBox.Show($"Domæne: {activeDirectory.Domain}{Environment.NewLine}Alle {adService.DepartmentLayout.Count} afdelings-OU'er blev fundet.{note}",
                    "Kontrol af domæne og OU'er", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // Usernames already in the database, minus the grid's own saved rows, so a
        // row reloaded at startup isn't flagged as a duplicate of itself.
        private List<string> GetExistingUsernames(IEnumerable<People> grid)
        {
            var savedIds = grid.Where(p => p.Id != 0).Select(p => p.Id).ToList();
            return dbContext.People.Where(p => !savedIds.Contains(p.Id)).Select(p => p.Username).ToList();
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            var people = batchPeople;
            ShowBatchView(); // so the results of the save are visible
            if (people.Count == 0)
            {
                MessageBox.Show("Der er ingen poster at gemme. Importér en CSV-fil først.", "Intet at gemme", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Re-checked here because rows may have been edited or added since import.
            RevalidateGrid(people);

            var validPeople = people.Where(p => p.ImportSuccess).ToList();
            int skipped = people.Count - validPeople.Count;

            if (validPeople.Count == 0)
            {
                MessageBox.Show("Intet blev gemt — ingen af posterne er gyldige. Se kolonnen Årsag.", "Intet at gemme", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // New rows go in as one import set. Rows already in the database are
            // tracked by dbContext, so SaveChanges also stores any edits made to them.
            var newPeople = validPeople.Where(p => p.Id == 0).ToList();
            try
            {
                if (newPeople.Count > 0)
                {
                    var import = new ImportSet { ImportDate = DateTime.Now, RecordCount = newPeople.Count };
                    import.People.AddRange(newPeople);
                    dbContext.ImportSets.Add(import);
                }
                dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                // Un-queue the rows that failed to insert; otherwise every later
                // SaveChanges (including LogAction below) retries them and fails too.
                foreach (var entry in dbContext.ChangeTracker.Entries().Where(en => en.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }
                foreach (var person in newPeople)
                {
                    person.Id = 0;
                    person.ImportSetId = 0;
                }

                // EF's own message just says "see the inner exception", so show that instead.
                string reason = ex.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"Fejl ved gemning i databasen: {reason}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
                LogAction("Gem fejlede", reason);
                return;
            }

            LogAction("Gem", $"{newPeople.Count} ny(e) post(er) gemt i databasen, {skipped} ugyldige sprunget over.");

            var pending = validPeople.Where(p => !p.CreatedInAD).ToList();
            if (pending.Count == 0)
            {
                MessageBox.Show($"Gemt i databasen. Alle {validPeople.Count} gyldige bruger(e) findes allerede i Active Directory.", "Gemt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveToAD = MessageBox.Show($"Gemt i databasen. Vil du også oprette {pending.Count} bruger(e) i Active Directory?", "Opret i Active Directory", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (saveToAD == MessageBoxResult.Yes)
            {
                SaveUsersToActiveDirectory(pending);
                dbContext.SaveChanges(); // stores each row's AD result (CreatedInAD, Status, Årsag)
            }
        }

        // Creates each person in the OU matching their department. Every row is
        // handled on its own so one failure doesn't stop the rest, and each outcome
        // is written to the activity log and the grid's Status/Reason columns.
        private void SaveUsersToActiveDirectory(List<People> people)
        {
            LogAction("AD-oprettelse startet", $"{people.Count} bruger(e), domæne '{activeDirectory.Domain}'.");
            int pdfCount = 0;
            string? pdfFolder = null; // created on the first PDF, so a run with no PDFs leaves no empty folder

            foreach (var person in people)
            {
                if (!adService.TryValidateDepartment(person.Department, out string ou))
                {
                    person.ImportSuccess = false;
                    person.ImportError = $"Ingen OU matcher afdelingen '{person.Department}'";
                    LogAction("AD-bruger sprunget over", $"{person.Username}: {person.ImportError}.");
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

                    person.CreatedInAD = true;
                    person.ImportSuccess = true;
                    person.ImportError = string.Empty;
                    LogAction("AD-bruger oprettet", $"{person.Username} i {ou}.");

                    // The account exists at this point, so a failure here is reported
                    // separately rather than marking the whole user as failed.
                    try
                    {
                        SetExtraAttributes(user, person);
                    }
                    catch (Exception ex)
                    {
                        person.ImportError = $"Oprettet, men afdeling/land blev ikke sat: {ex.Message}";
                        LogAction("AD-attributter fejlede", $"{person.Username}: {ex.GetType().Name}: {ex.Message}");
                    }

                    // Same here: the account is already created, so a PDF failure is only noted.
                    try
                    {
                        pdfFolder ??= pdfService.CreateRunFolder();
                        string pdfPath = pdfService.GeneratePDF(person, pdfFolder);
                        pdfCount++;
                        LogAction("PDF oprettet", $"{person.Username}: {pdfPath}");
                    }
                    catch (Exception ex)
                    {
                        person.ImportError = string.IsNullOrEmpty(person.ImportError)
                            ? $"Oprettet, men PDF fejlede: {ex.Message}"
                            : $"{person.ImportError}; PDF fejlede: {ex.Message}";
                        LogAction("PDF fejlede", $"{person.Username}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    string reason = DescribeADError(ex);
                    person.ImportSuccess = false;
                    person.ImportError = reason;
                    LogAction("AD-bruger fejlede", $"{person.Username} i {ou}: {reason}");
                }
            }

            int created = people.Count(p => p.ImportSuccess);
            int failed = people.Count - created;
            LogAction("AD-oprettelse afsluttet", $"{created} oprettet, {failed} fejlede eller sprunget over.");

            directoryUsers = null; // AD has changed, so read it again next time it's shown
            ShowView();
            string summary = failed == 0
                ? $"Oprettede {created} bruger(e) i Active Directory."
                : $"Oprettede {created} bruger(e). {failed} fejlede eller blev sprunget over. Se kolonnen Årsag eller menuen Log for detaljer.";
            if (pdfFolder != null)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{pdfCount} PDF(er) gemt i {pdfFolder}";
            }
            MessageBox.Show(summary, "Active Directory", MessageBoxButton.OK, failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

            // Opened after the message box so the folder isn't hidden behind it.
            if (pdfFolder != null)
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{pdfFolder}\"");
                }
                catch (Exception ex)
                {
                    LogAction("Kunne ikke åbne PDF-mappe", ex.Message);
                }
            }
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

        // AD's own message is often just "A constraint violation occurred". The
        // extended message underneath names the attribute it rejected, e.g.
        // "... Att 90290 (userPrincipalName)", so that's included when present.
        private static string DescribeADError(Exception ex)
        {
            string reason = $"{ex.GetType().Name}: {ex.Message.Trim()}";
            for (Exception? inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner.Message.Trim() != ex.Message.Trim())
                {
                    reason += $" ({inner.Message.Trim()})";
                }
                if (inner is System.DirectoryServices.DirectoryServicesCOMException com
                    && !string.IsNullOrWhiteSpace(com.ExtendedErrorMessage))
                {
                    reason += $" [{com.ExtendedErrorMessage.Trim().TrimEnd('\0')}]";
                }
            }
            return reason;
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

        // Adds a single person by hand, for when one new user doesn't warrant a CSV.
        private void OnAddPersonClicked(object sender, RoutedEventArgs e)
        {
            var person = new People();
            var editor = new Editor(person, adService, isNew: true) { Owner = this };
            if (editor.ShowDialog() != true)
            {
                return;
            }

            batchPeople.Add(person);
            ShowBatchView();
            RevalidateGrid(batchPeople);
            LogAction("Bruger tilføjet", person.Username);
        }

        // Opens the clicked row in the editor, e.g. to fix a rejected row in place.
        private void ListViewPeople_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Only the batch is editable; the overview views are read-only.
            if (currentView != UserView.Batch || ListViewPeople.SelectedItem is not People person)
            {
                return;
            }

            var editor = new Editor(person, adService, isNew: false) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                RevalidateGrid(batchPeople);
                LogAction("Bruger redigeret", person.Username);
            }
        }

        // Re-runs the same checks as import, so an edited or added row shows
        // whether it's now valid.
        private void RevalidateGrid(List<People> people)
        {
            MainWindowHelpers.ValidateForDuplicateUsernames(people, GetExistingUsernames(people));
            MainWindowHelpers.ValidateDepartments(people, d => adService.TryValidateDepartment(d, out _));
            MainWindowHelpers.ValidateForActiveDirectory(people);
            ShowView();
        }

        // --- Views and the department tree ---

        private void ShowBatchView()
        {
            if (ViewSelector.SelectedIndex != 0)
            {
                ViewSelector.SelectedIndex = 0; // the SelectionChanged handler shows it
            }
            else
            {
                ShowView();
            }
        }

        private async void ViewSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ready)
            {
                return;
            }

            currentView = (UserView)Math.Max(0, ViewSelector.SelectedIndex);
            RefreshButton.Visibility = currentView == UserView.Batch ? Visibility.Collapsed : Visibility.Visible;

            if (currentView == UserView.Directory && directoryUsers == null)
            {
                await LoadDirectoryUsersAsync();
            }
            ShowView();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentView == UserView.Directory)
            {
                await LoadDirectoryUsersAsync();
            }
            ShowView();
        }

        // Puts the chosen view's users in the grid, rebuilds the tree with fresh
        // counts and applies the selected department filter.
        private void ShowView()
        {
            if (!ready)
            {
                return;
            }

            System.Collections.IList items;
            if (currentView == UserView.Batch)
            {
                ListViewPeople.View = (ViewBase)FindResource("BatchGridView");
                items = batchPeople;
            }
            else
            {
                ListViewPeople.View = (ViewBase)FindResource("OverviewGridView");
                items = currentView == UserView.Saved ? LoadSavedUsers() : directoryUsers ?? new List<UserOverview>();
            }

            ListViewPeople.ItemsSource = items;
            var departments = items.Cast<object>().Select(DepartmentOf).ToList();
            BuildTree(departments);
            ApplyFilter(items.Count);
        }

        // Every user BatchBaker has saved, from all imports, newest import first.
        private List<UserOverview> LoadSavedUsers()
        {
            return dbContext.ImportSets
                .Include(i => i.People)
                .OrderByDescending(i => i.ImportDate)
                .AsEnumerable()
                .SelectMany(i => i.People.Select(p => new UserOverview
                {
                    Status = p.CreatedInAD ? "Oprettet i AD" : "Ikke oprettet i AD",
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Username = p.Username,
                    Email = p.Email,
                    Department = p.Department,
                    Country = p.Country,
                    PhoneNumber = p.PhoneNumber,
                    Created = i.ImportDate.ToString("dd-MM-yyyy HH:mm")
                }))
                .ToList();
        }

        private async Task LoadDirectoryUsersAsync()
        {
            if (string.IsNullOrEmpty(activeDirectory.Domain))
            {
                directoryUsers = new List<UserOverview>();
                directoryFailedOus = new List<string>();
                return;
            }

            StatusText.Text = "Henter brugere fra Active Directory…";
            RefreshButton.IsEnabled = false;
            ViewSelector.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var (users, failedOus) = await Task.Run(adService.GetDirectoryUsers);
                directoryUsers = users;
                directoryFailedOus = failedOus;
            }
            finally
            {
                Mouse.OverrideCursor = null;
                RefreshButton.IsEnabled = true;
                ViewSelector.IsEnabled = true;
            }
        }

        // The department a row belongs to, in its official spelling, or null if it
        // matches none (e.g. a misspelled department in the batch).
        private string? DepartmentOf(object item) => item switch
        {
            People p => adService.MatchDepartment(p.Department),
            UserOverview u => adService.MatchDepartment(u.Department),
            _ => null
        };

        // Rebuilds the tree as All > sectors > departments, each with a user count,
        // and selects the node that was selected before.
        private void BuildTree(List<string?> departments)
        {
            buildingTree = true;
            try
            {
                DepartmentTree.Items.Clear();
                var layout = adService.DepartmentLayout;

                var root = MakeNode($"Alle afdelinger ({departments.Count})", AllNode);
                foreach (var sector in layout.Select(d => d.Sector).Distinct())
                {
                    var sectorDepartments = layout.Where(d => d.Sector == sector).Select(d => d.Department).ToList();
                    int sectorCount = departments.Count(d => d != null && sectorDepartments.Contains(d));
                    var sectorNode = MakeNode($"{sector} ({sectorCount})", "sector:" + sector);
                    foreach (string department in sectorDepartments)
                    {
                        sectorNode.Items.Add(MakeNode($"{department} ({departments.Count(d => d == department)})", "dept:" + department));
                    }
                    root.Items.Add(sectorNode);
                }

                int unknown = departments.Count(d => d == null);
                if (unknown > 0)
                {
                    root.Items.Add(MakeNode($"Ukendt afdeling ({unknown})", UnknownNode));
                }
                DepartmentTree.Items.Add(root);

                var selected = FindNode(root, selectedNode) ?? root;
                selectedNode = (string)selected.Tag;
                selected.IsSelected = true;
            }
            finally
            {
                buildingTree = false;
            }

            static TreeViewItem MakeNode(string header, string key) => new() { Header = header, Tag = key, IsExpanded = true };
        }

        private static TreeViewItem? FindNode(TreeViewItem node, string key)
        {
            if ((string)node.Tag == key)
            {
                return node;
            }
            return node.Items.OfType<TreeViewItem>().Select(child => FindNode(child, key)).FirstOrDefault(found => found != null);
        }

        private void DepartmentTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (buildingTree || DepartmentTree.SelectedItem is not TreeViewItem node)
            {
                return;
            }
            selectedNode = (string)node.Tag;
            ApplyFilter(ListViewPeople.ItemsSource is System.Collections.IList list ? list.Count : 0);
        }

        // Shows only the rows in the selected sector or department.
        private void ApplyFilter(int total)
        {
            var layout = adService.DepartmentLayout;
            string node = selectedNode;
            ListViewPeople.Items.Filter = item =>
            {
                string? department = DepartmentOf(item);
                if (node == AllNode) return true;
                if (node == UnknownNode) return department == null;
                if (node.StartsWith("dept:")) return department == node.Substring(5);
                if (node.StartsWith("sector:")) return layout.Any(d => d.Sector == node.Substring(7) && d.Department == department);
                return true;
            };

            string status = $"Viser {ListViewPeople.Items.Count} af {total} bruger(e)";
            if (currentView == UserView.Directory)
            {
                if (string.IsNullOrEmpty(activeDirectory.Domain))
                {
                    status = "Computeren er ikke medlem af et domæne, så Active Directory kan ikke vises.";
                }
                else if (directoryFailedOus.Count > 0)
                {
                    status += $" – {directoryFailedOus.Count} OU('er) kunne ikke læses (se Administrér > Kontrollér domæne og OU'er)";
                }
            }
            StatusText.Text = status;
        }

        // --- Administrér, Hjælp ---

        private void OnOpenPdfFolderClicked(object sender, RoutedEventArgs e)
            => OpenFolder(pdfService.BaseFolder);

        private void OnOpenDataFolderClicked(object sender, RoutedEventArgs e)
            => OpenFolder(AppDbContext.DataFolder);

        private void OpenFolder(string folder)
        {
            try
            {
                System.IO.Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start("explorer.exe", $"\"{folder}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Mappen kunne ikke åbnes: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCheckOusClicked(object sender, RoutedEventArgs e)
            => CheckDomainAndOus(reportSuccess: true);

        private void OnAboutClicked(object sender, RoutedEventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            string domain = string.IsNullOrEmpty(activeDirectory.Domain) ? "(intet domæne fundet)" : activeDirectory.Domain;
            MessageBox.Show(
                $"BatchBaker {version?.ToString(3)}{Environment.NewLine}{Environment.NewLine}" +
                $"Opretter brugere i Active Directory ud fra CSV-filer, med genererede adgangskoder og en login-PDF til hver bruger.{Environment.NewLine}{Environment.NewLine}" +
                $"Domæne: {domain}{Environment.NewLine}" +
                $"Data: {AppDbContext.DataFolder}{Environment.NewLine}" +
                $"PDF'er: {pdfService.BaseFolder}",
                "Om BatchBaker", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ImportCSV_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV-filer (*.csv)|*.csv|Alle filer (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = openFileDialog.FileName;
                    var people = MainWindowHelpers.ReadCSV(filePath).ToList();
                    var existingUsernames = GetExistingUsernames(people);
                    MainWindowHelpers.ValidateForDuplicateUsernames(people, existingUsernames);
                    MainWindowHelpers.ValidateDepartments(people, d => adService.TryValidateDepartment(d, out _));
                    MainWindowHelpers.ValidateForActiveDirectory(people);

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

                    var previewWindow = new ImportPreviewWindow(people, existingUsernames, adService) { Owner = this };
                    if (previewWindow.ShowDialog() == true)
                    {
                        batchPeople = previewWindow.ValidPeople;
                        ShowBatchView();
                        int skipped = people.Count - previewWindow.ValidPeople.Count;
                        string message = skipped > 0
                            ? $"Importerede {previewWindow.ValidPeople.Count} post(er). {skipped} ugyldige sprunget over."
                            : $"{previewWindow.ValidPeople.Count} post(er) blev importeret.";
                        MessageBox.Show(message, "Import gennemført", MessageBoxButton.OK, MessageBoxImage.Information);
                        LogAction("Import", $"{System.IO.Path.GetFileName(filePath)}: {previewWindow.ValidPeople.Count} post(er) importeret, {skipped} sprunget over.");
                    }
                    else
                    {
                        LogAction("Import annulleret", System.IO.Path.GetFileName(filePath));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fejl ved indlæsning af CSV-fil: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogAction("Import fejlede", ex.Message);
                }
            }
        }

    }
}
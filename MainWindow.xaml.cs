using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using BatchBaker.Data;
using BatchBaker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace BatchBaker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public AppDbContext dbContext = new AppDbContext();
        public MainWindow()
        {
            InitializeComponent();

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
            dbContext.Database.EnsureCreated();
            var import = dbContext.ImportSets.Include(i => i.People).OrderByDescending(i => i.ImportDate).FirstOrDefault();
            if (import != null)
            {
                ListViewPeople.ItemsSource = import.People;
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            var people = (ListViewPeople.ItemsSource as IEnumerable<People>)?.ToList();
            if (people == null || people.Count == 0)
            {
                MessageBox.Show("No records loaded to save. Import a CSV file first.", "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Re-checked here (not just at Import) because the grid can also be
            // pre-populated from the last saved import set on startup, so a stale
            // batch shouldn't be able to re-save itself as duplicates.
            var existingUsernames = dbContext.People.Select(p => p.Username).ToList();
            MainWindowHelpers.ValidateForDuplicateUsernames(people, existingUsernames);

            var validPeople = people.Where(p => p.ImportSuccess).ToList();
            int skipped = people.Count - validPeople.Count;

            if (validPeople.Count == 0)
            {
                MessageBox.Show("Nothing was saved — every record's username already exists in the database.", "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var import = new ImportSet { ImportDate = DateTime.Now, RecordCount = validPeople.Count };
                dbContext.ImportSets.Add(import);
                dbContext.SaveChanges();
                import.People.AddRange(validPeople);
                dbContext.SaveChanges();

                string message = skipped > 0
                    ? $"Saved {validPeople.Count} record(s). Skipped {skipped} duplicate(s)."
                    : $"Successfully saved {validPeople.Count} record(s) to the database.";
                MessageBox.Show(message, "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving to database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCreateActiveDirectoryClicked(object sender, RoutedEventArgs e)
        {
            var people = (ListViewPeople.ItemsSource as IEnumerable<People>)?.ToList();
            if (people == null || people.Count == 0)
            {
                MessageBox.Show("No records loaded. Import a CSV file first.", "Nothing to Create", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ldapPath = AppSettings.Instance.ActiveDirectory.LdapPath;
            MainWindowHelpers.CreateActiveDirectoryUsers(people, ldapPath);

            int succeeded = people.Count(p => p.ImportSuccess);
            int failed = people.Count - succeeded;
            string summary = failed == 0
                ? $"Created {succeeded} user(s) in Active Directory."
                : $"Created {succeeded} user(s). {failed} failed — see the Reason column for details.";
            MessageBox.Show(summary, "Active Directory", MessageBoxButton.OK, MessageBoxImage.Information);

            ListViewPeople.Items.Refresh();
        }

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

                    var previewWindow = new ImportPreviewWindow(people) { Owner = this };
                    if (previewWindow.ShowDialog() == true)
                    {
                        ListViewPeople.ItemsSource = previewWindow.ValidPeople;
                        int skipped = people.Count - previewWindow.ValidPeople.Count;
                        string message = skipped > 0
                            ? $"Imported {previewWindow.ValidPeople.Count} record(s). Skipped {skipped} duplicate(s)."
                            : $"Successfully imported {previewWindow.ValidPeople.Count} record(s).";
                        MessageBox.Show(message, "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading CSV file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
}
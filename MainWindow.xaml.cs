using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace BatchBaker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            try
            {
                var repository = new PersonRepository();
                repository.EnsureCreated();
                ListViewPeople.ItemsSource = repository.LoadAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading saved users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            var people = ListViewPeople.ItemsSource as IEnumerable<Person>;
            if (people == null || !people.Any())
            {
                MessageBox.Show("No records loaded to save. Import a CSV file first.", "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var repository = new PersonRepository();
                repository.EnsureCreated();
                int count = repository.SaveAll(people);
                ListViewPeople.ItemsSource = repository.LoadAll();
                MessageBox.Show($"Successfully saved {count} record(s) to the database.", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving to database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            var selected = ListViewPeople.SelectedItems.Cast<Person>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select one or more users in the list before deleting.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmation = MessageBox.Show(
                $"Delete {selected.Count} selected user(s)? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var repository = new PersonRepository();
                var idsToDelete = selected.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToList();
                if (idsToDelete.Count > 0)
                {
                    repository.Delete(idsToDelete);
                }

                ListViewPeople.ItemsSource = repository.LoadAll();
                MessageBox.Show($"Deleted {selected.Count} user(s).", "Delete Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting user(s): {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                    var people = MainWindowHelpers.ReadCSV(filePath);
                    ListViewPeople.ItemsSource = people;
                    MessageBox.Show($"Successfully loaded {people.Count()} records from the CSV file.", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading CSV file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
}
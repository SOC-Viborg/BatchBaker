using System;
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
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            
        }

        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ClickHandler3(object sender, RoutedEventArgs e)
        {
            //do something

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
        public void SaveUsers_Click(object sender, RoutedEventArgs e)
        {
            
        }

    }
}
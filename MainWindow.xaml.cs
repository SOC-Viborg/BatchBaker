using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
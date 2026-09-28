using System.Collections.Generic;
using System.Linq;
using System.Windows;
using BatchBaker.Models;

namespace BatchBaker
{
    /// <summary>
    /// Lets the user inspect every row parsed from an imported CSV, see which rows
    /// are valid vs. flagged as duplicates, and confirm before anything reaches the
    /// main grid (and eventually the database).
    /// </summary>
    public partial class ImportPreviewWindow : Window
    {
        private readonly List<People> _people;

        public List<People> ValidPeople { get; private set; } = new();

        public ImportPreviewWindow(List<People> people)
        {
            InitializeComponent();
            _people = people;

            int validCount = people.Count(p => p.ImportSuccess);
            int invalidCount = people.Count - validCount;

            SummaryText.Text = invalidCount == 0
                ? $"{validCount} of {people.Count} row(s) are valid and ready to import."
                : $"{validCount} of {people.Count} row(s) are valid. {invalidCount} will be skipped due to duplicate usernames.";

            PreviewListView.ItemsSource = people;
            ImportButton.IsEnabled = validCount > 0;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            ValidPeople = _people.Where(p => p.ImportSuccess).ToList();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

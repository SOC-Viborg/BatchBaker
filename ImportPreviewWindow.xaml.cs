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
                ? $"{validCount} af {people.Count} række(r) er gyldige og klar til import."
                : $"{validCount} af {people.Count} række(r) er gyldige. {invalidCount} springes over på grund af dublerede brugernavne.";

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

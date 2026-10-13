using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using BatchBaker.Models;
using BatchBaker.Services;

namespace BatchBaker
{
    /// <summary>
    /// Lets the user inspect every row parsed from an imported CSV, see which rows
    /// are valid vs. rejected, fix rejected rows by double-clicking them, and
    /// confirm before anything reaches the main grid (and eventually the database).
    /// </summary>
    public partial class ImportPreviewWindow : Window
    {
        private readonly List<People> _people;
        private readonly List<string> _existingUsernames;
        private readonly ADService _adService;

        public List<People> ValidPeople { get; private set; } = new();

        public ImportPreviewWindow(List<People> people, List<string> existingUsernames, ADService adService)
        {
            InitializeComponent();
            _people = people;
            _existingUsernames = existingUsernames;
            _adService = adService;

            PreviewListView.ItemsSource = people;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int validCount = _people.Count(p => p.ImportSuccess);
            int invalidCount = _people.Count - validCount;

            SummaryText.Text = invalidCount == 0
                ? $"{validCount} af {_people.Count} række(r) er gyldige og klar til import."
                : $"{validCount} af {_people.Count} række(r) er gyldige. {invalidCount} springes over — dobbeltklik på en række for at rette den.";

            ImportButton.IsEnabled = validCount > 0;
        }

        private void PreviewListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PreviewListView.SelectedItem is not People person)
            {
                return;
            }

            var editor = new Editor(person, _adService, isNew: false) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                MainWindowHelpers.ValidateForDuplicateUsernames(_people, _existingUsernames);
                MainWindowHelpers.ValidateDepartments(_people, d => _adService.TryValidateDepartment(d, out _));
                PreviewListView.Items.Refresh();
                UpdateSummary();
            }
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

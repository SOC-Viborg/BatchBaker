using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BatchBaker.Models;
using BatchBaker.Services;

namespace BatchBaker
{
    /// <summary>
    /// Lets the user inspect rows, see which are valid vs. rejected, fix rejected rows
    /// by double-clicking them and discard the ones that shouldn't be created. Used
    /// before an imported CSV reaches the main grid, and again by Gem for users that
    /// were rejected (review mode).
    /// </summary>
    public partial class ImportPreviewWindow : Window
    {
        private readonly List<People> _people;
        private readonly List<string> _existingUsernames;
        private readonly ADService _adService;
        private readonly bool _reviewMode;

        // Why Active Directory rejected a row. The local checks can't see these, so
        // they're kept until the row is edited or discarded.
        private readonly Dictionary<People, string> _adRejections = new();

        public List<People> ValidPeople { get; private set; } = new();
        public List<People> EditedPeople { get; } = new();
        public List<People> DiscardedPeople { get; } = new();

        public ImportPreviewWindow(List<People> people, List<string> existingUsernames, ADService adService, bool reviewMode = false, bool rejectedByAD = false)
        {
            InitializeComponent();
            _people = people;
            _existingUsernames = existingUsernames;
            _adService = adService;
            _reviewMode = reviewMode;

            if (rejectedByAD)
            {
                foreach (var person in people)
                {
                    _adRejections[person] = person.ImportError;
                }
            }

            if (reviewMode)
            {
                Title = "Ret afviste brugere";
                ImportButton.Content = "Fortsæt";
            }

            PreviewListView.ItemsSource = people;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int validCount = _people.Count(p => p.ImportSuccess);
            int invalidCount = _people.Count - validCount;

            if (_reviewMode)
            {
                SummaryText.Text = invalidCount == 0
                    ? "Alle rækker er klar. Klik Fortsæt."
                    : $"{invalidCount} bruger(e) blev afvist. Dobbeltklik på en række for at rette den, eller markér dem, der ikke skal oprettes, og klik Kassér valgte.";
                ImportButton.IsEnabled = true;
            }
            else
            {
                SummaryText.Text = invalidCount == 0
                    ? $"{validCount} af {_people.Count} række(r) er gyldige og klar til import."
                    : $"{validCount} af {_people.Count} række(r) er gyldige. {invalidCount} springes over — dobbeltklik på en række for at rette den, eller kassér den.";
                ImportButton.IsEnabled = validCount > 0;
            }
        }

        // Re-runs the checks for every row, since fixing or discarding one row can
        // also clear a duplicate on another.
        private void Revalidate()
        {
            MainWindowHelpers.ValidateForDuplicateUsernames(_people, _existingUsernames);
            MainWindowHelpers.ValidateDepartments(_people, d => _adService.TryValidateDepartment(d, out _));
            MainWindowHelpers.ValidateForActiveDirectory(_people);

            foreach (var (person, reason) in _adRejections)
            {
                person.ImportSuccess = false;
                person.ImportError = reason;
            }

            PreviewListView.Items.Refresh();
            UpdateSummary();
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
                _adRejections.Remove(person);
                if (!EditedPeople.Contains(person))
                {
                    EditedPeople.Add(person);
                }
                Revalidate();
            }
        }

        private void PreviewListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DiscardButton.IsEnabled = PreviewListView.SelectedItems.Count > 0;
        }

        private void DiscardButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var person in PreviewListView.SelectedItems.OfType<People>().ToList())
            {
                _people.Remove(person);
                _adRejections.Remove(person);
                EditedPeople.Remove(person);
                DiscardedPeople.Add(person);
            }
            Revalidate();
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using BatchBaker.Models;
using BatchBaker.Services;

namespace BatchBaker
{
    /// <summary>
    /// Edits a single person in place: fixing a row that was rejected (e.g. a
    /// misspelled department) or filling in a brand-new one, so the user doesn't
    /// have to go back and edit the CSV. Changes are only written to the person
    /// when Gem is clicked; Annullér leaves it untouched.
    /// </summary>
    public partial class Editor : Window
    {
        private readonly People _person;

        public Editor(People person, ADService adService, bool isNew)
        {
            InitializeComponent();
            _person = person;
            Title = isNew ? "Tilføj bruger" : "Redigér bruger";

            if (!isNew && !person.ImportSuccess && !string.IsNullOrWhiteSpace(person.ImportError))
            {
                RejectReasonText.Text = $"Afvist: {person.ImportError}";
                RejectReasonText.Visibility = Visibility.Visible;
            }

            FirstNameBox.Text = person.FirstName;
            LastNameBox.Text = person.LastName;
            UsernameBox.Text = person.Username;
            EmailBox.Text = person.Email;
            CountryBox.Text = string.IsNullOrWhiteSpace(person.Country) ? "Denmark" : person.Country;
            PhoneBox.Text = person.PhoneNumber;
            PasswordBox.Text = person.TemporaryPassword;

            // A dropdown instead of free text, so the department always matches an OU.
            // A misspelled value matches nothing and is left blank for the user to pick.
            DepartmentBox.ItemsSource = adService.DepartmentNames;
            DepartmentBox.SelectedItem = adService.MatchDepartment(person.Department);

            Loaded += (_, _) => FirstNameBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            {
                problems.Add("Brugernavn skal udfyldes.");
            }
            if (DepartmentBox.SelectedItem == null)
            {
                problems.Add("Vælg en afdeling.");
            }
            if (string.IsNullOrWhiteSpace(PasswordBox.Text))
            {
                problems.Add("Midlertidig adgangskode skal udfyldes.");
            }

            if (problems.Count > 0)
            {
                ValidationText.Text = string.Join(Environment.NewLine, problems);
                ValidationText.Visibility = Visibility.Visible;
                return;
            }

            _person.FirstName = FirstNameBox.Text.Trim();
            _person.LastName = LastNameBox.Text.Trim();
            _person.Username = UsernameBox.Text.Trim();
            _person.Email = EmailBox.Text.Trim();
            _person.Department = (string)DepartmentBox.SelectedItem!;
            _person.Country = CountryBox.Text.Trim();
            _person.PhoneNumber = PhoneBox.Text.Trim();
            _person.TemporaryPassword = PasswordBox.Text.Trim();

            DialogResult = true;
        }
    }
}

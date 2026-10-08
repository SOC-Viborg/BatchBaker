using System.Collections.Generic;
using System.Windows;
using BatchBaker.Models;

namespace BatchBaker
{
    /// <summary>
    /// Displays recorded activity log entries, newest first.
    /// </summary>
    public partial class LogViewerWindow : Window
    {
        public LogViewerWindow(List<ActivityLog> logs)
        {
            InitializeComponent();
            LogListView.ItemsSource = logs;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

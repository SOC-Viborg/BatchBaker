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
using System.DirectoryServices.ActiveDirectory;
using System.Security.Cryptography.X509Certificates;
using System.DirectoryServices;

    namespace BatchBaker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string _serverName = "your_server_name"; // Replace with your server name
        private static string _username = "your_username"; // Replace with your username
        private static string _password = "your_password"; // Replace with your password
        private static string _domain = "your_domain"; // Replace with your domain
        
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
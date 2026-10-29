namespace BatchBaker.Models
{
    // A read-only row for the overview views in the main window: users saved by
    // BatchBaker, or users read from Active Directory.
    public class UserOverview
    {
        public string Status { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Created { get; set; } = string.Empty;
    }
}

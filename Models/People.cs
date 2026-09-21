using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatchBaker.Models
{
    public class People
    {
        [Key]
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;

        public int ImportSetId { get; set; }
        [NotMapped]
        public ImportSet ImportSet { get; set; } = default!;

    }
}

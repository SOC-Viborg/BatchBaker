using System.ComponentModel.DataAnnotations;

namespace BatchBaker.Models
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}

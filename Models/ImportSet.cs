using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatchBaker.Models
{
    public class ImportSet
    {
        [Key]
        public int Id { get; set; }
        public DateTime ImportDate { get; set; }
        public int RecordCount { get; set; }

        [NotMapped]
        public List<People> People { get; set; } = new List<People>();
    }
}

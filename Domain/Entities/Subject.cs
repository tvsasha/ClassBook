using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassBook.Domain.Entities
{
    public class Subject
    {
        [Key]
        public int SubjectId { get; set; }
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = null!;
        [Required]
        public int TeacherId { get; set; }
        public User Teacher { get; set; } = null!;
        public ICollection<Lesson>? Lessons { get; set; }
        public ICollection<SubjectClassAssignment>? ClassAssignments { get; set; }
    }
}

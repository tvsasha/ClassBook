using System;

namespace ClassBook.Domain.Entities
{
    /// <summary>
    /// Связь между ученником и родителем
    /// Один ученик может иметь нескольких родителей, один родитель может иметь нескольких учеников
    /// </summary>
    public class StudentParent
    {
        public int StudentParentId { get; set; }
        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        /// <summary>
        /// ID родителя (User с ролью "Родитель")
        /// </summary>
        public int ParentId { get; set; }
        public User Parent { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

namespace ClassBook.Domain.Entities
{
    public class SubjectClassAssignment
    {
        public int SubjectClassAssignmentId { get; set; }
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
        public int ClassId { get; set; }
        public Class Class { get; set; } = null!;
        public int TeacherId { get; set; }
        public User Teacher { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

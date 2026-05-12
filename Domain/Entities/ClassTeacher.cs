namespace ClassBook.Domain.Entities
{
    public class ClassTeacher
    {
        public int ClassTeacherId { get; set; }
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Class Class { get; set; } = null!;
        public User Teacher { get; set; } = null!;
    }
}

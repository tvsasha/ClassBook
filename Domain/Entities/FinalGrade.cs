namespace ClassBook.Domain.Entities
{
    public class FinalGrade
    {
        public int FinalGradeId { get; set; }
        public int AcademicPeriodId { get; set; }
        public AcademicPeriod AcademicPeriod { get; set; } = null!;
        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
        public int Value { get; set; }
        public int SetByUserId { get; set; }
        public User SetByUser { get; set; } = null!;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

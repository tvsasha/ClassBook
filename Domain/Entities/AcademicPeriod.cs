namespace ClassBook.Domain.Entities
{
    public class AcademicPeriod
    {
        public int AcademicPeriodId { get; set; }
        public int AcademicYearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
        public ICollection<FinalGrade> FinalGrades { get; set; } = [];
    }
}

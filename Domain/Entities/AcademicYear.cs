namespace ClassBook.Domain.Entities
{
    public class AcademicYear
    {
        public int AcademicYearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public ICollection<AcademicPeriod> Periods { get; set; } = [];
    }
}

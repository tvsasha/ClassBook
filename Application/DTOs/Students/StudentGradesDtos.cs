namespace ClassBook.Application.DTOs
{
    public class GradeValueDto
    {
        public int GradeId { get; set; }
        public int Value { get; set; }
    }

    public class StudentGradesForLessonDto
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public List<GradeValueDto> Grades { get; set; } = [];
    }
}

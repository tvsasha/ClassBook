namespace ClassBook.Application.DTOs
{
    public class CreateLessonDto
    {
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    public class AddGradeDto
    {
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public int Value { get; set; }
    }

    public class MarkAttendanceDto
    {
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public byte Status { get; set; }
    }
}

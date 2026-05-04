namespace ClassBook.Application.DTOs
{
    public class AddGradeRequest
    {
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public int Value { get; set; }
    }

    public class GradeDto
    {
        public int GradeId { get; set; }
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public int Value { get; set; }
        public StudentGradeOwnerDto? Student { get; set; }
    }

    public class StudentGradeOwnerDto
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class TeacherGradeListItemDto
    {
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public int Value { get; set; }
        public TeacherGradeStudentDto? Student { get; set; }
        public TeacherGradeLessonDto? Lesson { get; set; }
    }

    public class TeacherGradeStudentDto
    {
        public string FullName { get; set; } = string.Empty;
    }

    public class TeacherGradeLessonDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
    }

    public class GradeAuditDto
    {
        public int GradeId { get; set; }
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public int Value { get; set; }
    }
}

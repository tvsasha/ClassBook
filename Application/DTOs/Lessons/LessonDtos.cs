namespace ClassBook.Application.DTOs
{
    public class CreateLessonRequest
    {
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    public class LessonResponse
    {
        public int LessonId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    public class TeacherLessonListItemDto
    {
        public int LessonId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    public class LessonAuditDto
    {
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }
}

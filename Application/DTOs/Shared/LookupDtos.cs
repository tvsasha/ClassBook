namespace ClassBook.Application.DTOs
{
    public class ClassListItemDto
    {
        public int ClassId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TeacherLookupDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
    }

    public class SubjectLookupDto
    {
        public int SubjectId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SubjectClassAssignmentDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
    }

    public class SubjectAdminResponseDto
    {
        public int SubjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
    }
}

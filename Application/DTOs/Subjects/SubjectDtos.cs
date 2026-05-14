namespace ClassBook.Application.DTOs
{
    public class CreateSubjectDto
    {
        public string Name { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public int? ClassId { get; set; }
    }

    public class AttachTeacherDto
    {
        public int TeacherId { get; set; }
    }

    public class SubjectAdminListItemDto
    {
        public int SubjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public List<SubjectClassAssignmentDto> ClassAssignments { get; set; } = [];
    }

    public class SubjectClassAssignmentRequestDto
    {
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
    }

    public class SubjectTeacherAttachResultDto
    {
        public string Message { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
    }
}

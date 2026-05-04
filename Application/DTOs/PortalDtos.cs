namespace ClassBook.Application.DTOs
{
    public class PortalStudentInfoDto
    {
        public int StudentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public PortalClassDto Class { get; set; } = new();
    }

    public class PortalClassDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class PortalScheduleEntryDto
    {
        public int LessonId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Topic { get; set; }
        public string? Homework { get; set; }
        public int? ScheduleId { get; set; }
        public int? LessonNumber { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
    }

    public class PortalGradeEntryDto
    {
        public int GradeId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? Teacher { get; set; }
        public int Value { get; set; }
        public DateTime Date { get; set; }
        public string? Topic { get; set; }
    }

    public class PortalHomeworkEntryDto
    {
        public int LessonId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Topic { get; set; }
        public string Homework { get; set; } = string.Empty;
    }

    public class PortalAttendanceEntryDto
    {
        public int LessonId { get; set; }
        public int? AttendanceId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public byte? Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Topic { get; set; }
    }

    public class PortalStudentReferenceDto
    {
        public int StudentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
    }

    public class PortalParentReferenceDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
    }

    public class PortalStudentParentDetailDto
    {
        public int StudentParentId { get; set; }
        public PortalStudentReferenceDto Student { get; set; } = new();
        public PortalParentReferenceDto Parent { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}

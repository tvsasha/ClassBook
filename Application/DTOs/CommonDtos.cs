namespace ClassBook.Application.DTOs
{
    public class MessageResponseDto
    {
        public string Message { get; set; } = string.Empty;
    }

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

    public class ScheduleEditorSubjectDto
    {
        public int SubjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
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

    public class UserListItemDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuthUserDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }

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

    public class ScheduleSlotDto
    {
        public int ScheduleId { get; set; }
        public int DayOfWeek { get; set; }
        public int LessonNumber { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
    }

    public class ScheduleEditorMetadataDto
    {
        public List<ClassListItemDto> Classes { get; set; } = [];
        public List<ScheduleEditorSubjectDto> Subjects { get; set; } = [];
        public List<TeacherLookupDto> Teachers { get; set; } = [];
        public List<ScheduleSlotDto> Slots { get; set; } = [];
    }

    public class ScheduleEditorLessonDto
    {
        public int LessonId { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public int? ScheduleId { get; set; }
        public int? DayOfWeek { get; set; }
        public int? LessonNumber { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Topic { get; set; }
        public string? Homework { get; set; }
        public DateTime Date { get; set; }
    }

    public class ScheduleEditorWeekDto
    {
        public DateTime WeekStart { get; set; }
        public List<ScheduleEditorLessonDto> Lessons { get; set; } = [];
    }
}

namespace ClassBook.Application.DTOs
{
    public class ScheduleEditorSubjectDto
    {
        public int SubjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
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

    public class ScheduleEditorLessonAuditDto
    {
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public int ScheduleId { get; set; }
        public string Topic { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    public class ScheduleEditorLessonMutationResultDto
    {
        public ScheduleEditorLessonDto Lesson { get; set; } = new();
        public ScheduleEditorLessonAuditDto? OldValues { get; set; }
        public ScheduleEditorLessonAuditDto NewValues { get; set; } = new();
    }

    public class ScheduleEditorLessonDeleteResultDto
    {
        public int LessonId { get; set; }
        public ScheduleEditorLessonAuditDto OldValues { get; set; } = new();
    }

    public class ScheduleSlotAuditDto
    {
        public int ScheduleId { get; set; }
        public int DayOfWeek { get; set; }
        public int LessonNumber { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    public class CreateScheduleRequest
    {
        public int DayOfWeek { get; set; }
        public int LessonNumber { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
    }

    public class UpdateScheduleRequest
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
    }

    public class ScheduleEditorLessonRequest
    {
        public int ClassId { get; set; }
        public int SubjectId { get; set; }
        public int TeacherId { get; set; }
        public int ScheduleId { get; set; }
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    public class ScheduleEditorClassRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}

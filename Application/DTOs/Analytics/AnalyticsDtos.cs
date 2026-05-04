namespace ClassBook.Application.DTOs
{
    public sealed class DailyCompletionLessonDto
    {
        public int LessonId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Teacher { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int GradesFormed { get; set; }
        public int AttendanceRecorded { get; set; }
        public int TotalStudents { get; set; }
        public double GradesPercentage { get; set; }
        public double AttendancePercentage { get; set; }
    }

    public sealed class DailyCompletionReportDto
    {
        public DateTime Date { get; set; }
        public int TotalLessons { get; set; }
        public int LessonsWithCompleteGrades { get; set; }
        public int LessonsWithCompleteAttendance { get; set; }
        public List<DailyCompletionLessonDto> Report { get; set; } = [];
    }

    public sealed class AttendanceStatisticsItemDto
    {
        public string ClassName { get; set; } = string.Empty;
        public int TotalStudents { get; set; }
        public int Present { get; set; }
        public int Absent { get; set; }
        public int Excused { get; set; }
        public double PresentPercentage { get; set; }
        public double AbsentPercentage { get; set; }
    }

    public sealed class AttendanceStatisticsReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<AttendanceStatisticsItemDto> Statistics { get; set; } = [];
    }

    public sealed class ProblematicStudentDto
    {
        public int StudentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public int Absences { get; set; }
        public double AbsencePercentage { get; set; }
        public double AverageGrade { get; set; }
        public int LowGrades { get; set; }
        public int TotalGrades { get; set; }
    }

    public sealed class ProblematicStudentsReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ProblematicStudentDto> ProblematicStudents { get; set; } = [];
    }

    public sealed class TeacherSubjectProgressDto
    {
        public string Subject { get; set; } = string.Empty;
        public int LessonCount { get; set; }
        public int GradesEntered { get; set; }
        public int AttendanceRecorded { get; set; }
    }

    public sealed class TeacherProgressReportDto
    {
        public int TeacherId { get; set; }
        public string Teacher { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalLessons { get; set; }
        public int LessonsWithCompleteGrades { get; set; }
        public int LessonsWithCompleteAttendance { get; set; }
        public double CompletionRateGrades { get; set; }
        public double CompletionRateAttendance { get; set; }
        public int TotalGradesEntered { get; set; }
        public int TotalAttendanceRecorded { get; set; }
        public List<TeacherSubjectProgressDto> SubjectStatistics { get; set; } = [];
    }

    public sealed class ClassSummaryItemDto
    {
        public string ClassName { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public double AverageAbsences { get; set; }
        public double AverageGrade { get; set; }
    }

    public sealed class ClassSummaryReportDto
    {
        public string Period { get; set; } = string.Empty;
        public List<ClassSummaryItemDto> ClassSummary { get; set; } = [];
    }
}

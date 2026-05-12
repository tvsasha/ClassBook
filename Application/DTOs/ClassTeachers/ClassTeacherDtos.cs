namespace ClassBook.Application.DTOs
{
    public class ClassTeacherAssignmentDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
    }

    public class AssignClassTeacherRequest
    {
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
    }

    public class ClassTeacherSubjectSummaryDto
    {
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public int LessonsCount { get; set; }
        public int GradesCount { get; set; }
        public double AverageGrade { get; set; }
        public int AbsencesCount { get; set; }
    }

    public class ClassTeacherStudentSummaryDto
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public double AverageGrade { get; set; }
        public int GradesCount { get; set; }
        public int AbsencesCount { get; set; }
    }

    public class ClassTeacherClassSummaryDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int StudentsCount { get; set; }
        public int LessonsCount { get; set; }
        public int GradesCount { get; set; }
        public double AverageGrade { get; set; }
        public int AbsencesCount { get; set; }
        public List<ClassTeacherSubjectSummaryDto> Subjects { get; set; } = [];
        public List<ClassTeacherStudentSummaryDto> Students { get; set; } = [];
    }

    public class ClassTeacherDashboardDto
    {
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public List<ClassTeacherClassSummaryDto> Classes { get; set; } = [];
        public List<TeacherLessonListItemDto> OwnLessons { get; set; } = [];
    }
}

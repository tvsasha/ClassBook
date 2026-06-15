namespace ClassBook.Application.DTOs
{
    public class AcademicYearDto
    {
        public int AcademicYearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public List<AcademicPeriodDto> Periods { get; set; } = [];
    }

    public class AcademicPeriodDto
    {
        public int AcademicPeriodId { get; set; }
        public int AcademicYearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
    }

    public class SaveAcademicYearDto
    {
        public int? AcademicYearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class SaveAcademicPeriodDto
    {
        public int? AcademicPeriodId { get; set; }
        public int AcademicYearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
    }

    public class SetFinalGradeDto
    {
        public int AcademicPeriodId { get; set; }
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public int Value { get; set; }
    }

    public class FinalGradeItemDto
    {
        public int? FinalGradeId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public int? Value { get; set; }
        public double CurrentAverage { get; set; }
        public int CurrentGradesCount { get; set; }
        public bool CanEdit { get; set; }
    }

    public class StudentFinalGradesDto
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int? ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public AcademicPeriodDto Period { get; set; } = new();
        public List<FinalGradeItemDto> Grades { get; set; } = [];
    }

    public class FinalGradeClassDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public bool IsClassTeacher { get; set; }
    }
}

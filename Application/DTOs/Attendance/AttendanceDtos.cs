namespace ClassBook.Application.DTOs
{
    public class MarkAttendanceRequest
    {
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public byte Status { get; set; }
    }

    public class LessonAttendanceDto
    {
        public int AttendanceId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public byte Status { get; set; }
        public int LessonId { get; set; }
    }
}

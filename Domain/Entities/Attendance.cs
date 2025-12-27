using System;
using System.ComponentModel.DataAnnotations;

namespace ClassBook.Domain.Entities
{
    /// <summary>
    /// Посещаемость ученика на уроке
    /// </summary>
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }
        [Required]
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; } = null!;
        [Required]
        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;
        [Required]
        public byte Status { get; set; }
    }
}

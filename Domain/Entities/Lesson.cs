using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassBook.Domain.Entities
{
    /// <summary>
    /// Урок по предмету в классе, с темой и ДЗ
    /// </summary>
    public class Lesson
    {
        [Key]
        public int LessonId { get; set; }
        [Required]
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
        [Required]
        public int ClassId { get; set; }
        public Class Class { get; set; } = null!;
        [Required]
        public int TeacherId { get; set; }
        public User Teacher { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string Topic { get; set; } = null!;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
        public ICollection<Grade>? Grades { get; set; }
        public ICollection<Attendance>? Attendances { get; set; }
    }
}

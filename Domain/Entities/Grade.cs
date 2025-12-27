using System;
using System.ComponentModel.DataAnnotations;

namespace ClassBook.Domain.Entities
{
    /// <summary>
    /// Оценка ученика за конкретный урок
    /// </summary>
    public class Grade
    {
        [Key]
        public int GradeId { get; set; }
        [Required]
        public int Value { get; set; } // 1-5
        [Required]
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; } = null!;
        [Required]
        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;
    }
}

using System;
using System.Collections.Generic;

namespace ClassBook.Domain.Entities
{
    /// <summary>
    /// Фиксированный слот расписания (день недели и номер урока)
    /// </summary>
    public class Schedule
    {
        public int ScheduleId { get; set; }
        /// <summary>
        /// День недели (0 = Пн, 1 = Вт, ... 4 = Пт)
        /// </summary>
        public int DayOfWeek { get; set; }
        /// <summary>
        /// Номер урока (1-10)
        /// </summary>
        public int LessonNumber { get; set; }
        /// <summary>
        /// Время начала урока
        /// </summary>
        public TimeSpan StartTime { get; set; }
        /// <summary>
        /// Время окончания урока
        /// </summary>
        public TimeSpan EndTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Навигация: уроки в этом temportal слоте
        /// </summary>
        public ICollection<Lesson>? Lessons { get; set; }
    }
}

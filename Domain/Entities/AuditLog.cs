using System;

namespace ClassBook.Domain.Entities
{
    /// <summary>
    /// Лог аудита для отслеживания всех изменений в системе
    /// </summary>
    public class AuditLog
    {
        public int AuditLogId { get; set; }
        /// <summary>
        /// ID пользователя, который выполнил действие
        /// </summary>
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>
        /// Тип сущности (Lesson, Grade, Schedule, Attendance)
        /// </summary>
        public string EntityType { get; set; } = null!;
        /// <summary>
        /// ID сущности
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// Действие (Create, Update, Delete)
        /// </summary>
        public string Action { get; set; } = null!;

        /// <summary>
        /// Старые значения в JSON формате (для Update/Delete)
        /// </summary>
        public string? OldValues { get; set; }
        /// <summary>
        /// Новые значения в JSON формате (для Create/Update)
        /// </summary>
        public string? NewValues { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

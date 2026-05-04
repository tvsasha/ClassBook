namespace ClassBook.Application.DTOs
{
    public class AuditEntryDetailDto
    {
        public int AuditLogId { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public object? OldValues { get; set; }
        public object? NewValues { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

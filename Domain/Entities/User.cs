namespace ClassBook.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public bool MustChangePassword { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Subject>? Subjects { get; set; }
        public ICollection<Lesson>? Lessons { get; set; }
        public Student? Student { get; set; }
        public ICollection<ClassTeacher>? ClassTeacherAssignments { get; set; }
        public ICollection<SubjectClassAssignment>? SubjectClassAssignments { get; set; }
        /// <summary>
        /// Навигация: для родителей - их дети
        /// </summary>
        public ICollection<StudentParent>? StudentParents { get; set; }
        /// <summary>
        /// Навигация: логи аудита этого пользователя
        /// </summary>
        public ICollection<AuditLog>? AuditLogs { get; set; }
    }
}

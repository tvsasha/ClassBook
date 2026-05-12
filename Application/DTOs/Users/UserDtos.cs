namespace ClassBook.Application.DTOs
{
    public class UserListItemDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUserDto
    {
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int RoleId { get; set; }
    }

    public class UpdateUserDto
    {
        public string? Login { get; set; }
        public string? FullName { get; set; }
        public string? Password { get; set; }
        public int? RoleId { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ParentStudentListItemDto
    {
        public int StudentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public int? ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
    }

    public class UserMutationAuditDto
    {
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public bool PasswordReset { get; set; }
    }

    public class UserCreateResultDto
    {
        public UserListItemDto User { get; set; } = new();
        public UserMutationAuditDto AuditValues { get; set; } = new();
    }

    public class UserUpdateResultDto
    {
        public UserListItemDto User { get; set; } = new();
        public UserMutationAuditDto OldValues { get; set; } = new();
        public UserMutationAuditDto NewValues { get; set; } = new();
    }

    public class UserDeleteResultDto
    {
        public int UserId { get; set; }
        public UserMutationAuditDto OldValues { get; set; } = new();
    }

    public class IssuedAccessDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

namespace ClassBook.Application.DTOs
{
    public class CreateStudentDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public int? ClassId { get; set; }
    }

    public class CreateStudentAccountDto
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AttachStudentAccountDto
    {
        public int UserId { get; set; }
    }

    public class CreateParentAccountDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AttachStudentToParentDto
    {
        public int ParentId { get; set; }
        public int StudentId { get; set; }
    }

    public class AdminStudentDto
    {
        public int StudentId { get; set; }
        public int? UserId { get; set; }
        public bool HasAccount { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public int ClassId { get; set; }
        public string? ClassName { get; set; }
    }

    public class IssuedStudentAccountDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class IssuedParentAccountDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class StudentAuditDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public int? ClassId { get; set; }
    }

    public class StudentAccessAuditDto
    {
        public int StudentId { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }
    }

    public class StudentParentLinkAuditDto
    {
        public int ParentId { get; set; }
        public int StudentId { get; set; }
    }

    public class StudentDeleteAuditDto
    {
        public int StudentId { get; set; }
    }
}

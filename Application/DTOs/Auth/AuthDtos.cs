namespace ClassBook.Application.DTOs
{
    public class AuthUserDto
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}

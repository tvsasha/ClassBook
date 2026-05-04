using ClassBook.Domain.Entities;

public class Student
{
    public int StudentId { get; set; }
    public int? UserId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateTime BirthDate { get; set; }
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<Grade>? Grades { get; set; }
    public ICollection<Attendance>? Attendances { get; set; }
    /// <summary>
    /// Навигация: родители этого ученика
    /// </summary>
    public ICollection<StudentParent>? Parents { get; set; }
}

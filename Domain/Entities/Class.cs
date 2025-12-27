namespace ClassBook.Domain.Entities
{
    /// <summary>
    /// Класс (например, 10А, 11Б)
    /// </summary>
    public class Class
    {
        public int ClassId { get; set; }
        public string Name { get; set; } = null!;
        public ICollection<Student>? Students { get; set; }
        public ICollection<Lesson>? Lessons { get; set; }
    }
}

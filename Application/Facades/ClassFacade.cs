// Application/Facades/ClassFacade.cs
using ClassBook.Application.DTOs;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для управления классами и учениками.
    /// </summary>
    public class ClassFacade
    {
        private readonly AppDbContext _db;

        public ClassFacade(AppDbContext db) => _db = db;

        /// <summary>
        /// Создаёт новый класс.
        /// </summary>
        public async Task<Class> CreateClassAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название класса обязательно");

            var classEntity = new Class { Name = name };
            _db.Classes.Add(classEntity);
            await _db.SaveChangesAsync();
            return classEntity;
        }

        public async Task<IEnumerable<ClassListItemDto>> GetClassesForTeacherAsync(int teacherId)
        {
            return await _db.Lessons
                .Where(l => l.TeacherId == teacherId)
                .Select(l => l.Class)
                .Distinct()
                .Select(c => new ClassListItemDto
                {
                    ClassId = c.ClassId,
                    Name = c.Name
                })
                .ToListAsync();
        }

        /// <summary>
        /// Назначает ученика в класс.
        /// </summary>
        public async Task AssignStudentToClassAsync(int studentId, int classId)
        {
            var student = await _db.Students.FindAsync(studentId);
            if (student == null) throw new KeyNotFoundException("Ученик не найден");

            var classEntity = await _db.Classes.FindAsync(classId);
            if (classEntity == null) throw new KeyNotFoundException("Класс не найден");

            student.ClassId = classId;
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Удаляет ученика из класса.
        /// </summary>
        public async Task RemoveStudentFromClassAsync(int studentId)
        {
            var student = await _db.Students.FindAsync(studentId);
            if (student == null) throw new KeyNotFoundException("Ученик не найден");

            student.ClassId = 0;
            await _db.SaveChangesAsync();
        }
    }
}

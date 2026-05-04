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
        public async Task<IEnumerable<ClassListItemDto>> GetAllClassesAsync()
        {
            return await _db.Classes
                .OrderBy(c => c.Name)
                .Select(c => new ClassListItemDto
                {
                    ClassId = c.ClassId,
                    Name = c.Name
                })
                .ToListAsync();
        }

        public async Task<ClassListItemDto> CreateClassAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название класса обязательно");

            var normalizedName = name.Trim();
            if (await _db.Classes.AnyAsync(c => c.Name == normalizedName))
                throw new InvalidOperationException("Класс с таким названием уже существует");

            var classEntity = new Class { Name = normalizedName };
            _db.Classes.Add(classEntity);
            await _db.SaveChangesAsync();

            return new ClassListItemDto
            {
                ClassId = classEntity.ClassId,
                Name = classEntity.Name
            };
        }

        public async Task DeleteClassAsync(int classId)
        {
            var classEntity = await _db.Classes.FindAsync(classId);
            if (classEntity == null)
                throw new KeyNotFoundException("Класс не найден");

            if (await _db.Students.AnyAsync(s => s.ClassId == classId) ||
                await _db.Lessons.AnyAsync(l => l.ClassId == classId))
            {
                throw new InvalidOperationException("Нельзя удалить класс с привязанными учениками или уроками");
            }

            _db.Classes.Remove(classEntity);
            await _db.SaveChangesAsync();
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

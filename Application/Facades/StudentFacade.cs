using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для управления учениками.
    /// </summary>
    public class StudentFacade
    {
        private readonly AppDbContext _db;

        public StudentFacade(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Создаёт нового ученика.
        /// </summary>
        /// <param name="firstName">Имя</param>
        /// <param name="lastName">Фамилия</param>
        /// <param name="birthDate">Дата рождения</param>
        /// <param name="classId">ID класса (опционально)</param>
        /// <returns>Созданный ученик</returns>
        public async Task<Student> CreateStudentAsync(string firstName, string lastName, DateTime birthDate, int? classId = null)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Имя и фамилия обязательны");

            if (classId.HasValue && !await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            var student = new Student
            {
                FirstName = firstName,
                LastName = lastName,
                BirthDate = birthDate,
                ClassId = classId ?? 0
            };

            _db.Students.Add(student);
            await _db.SaveChangesAsync();
            return student;
        }

        public async Task<Student> UpdateStudentAsync(int id, string firstName, string lastName, DateTime birthDate, int? classId)
        {
            var student = await _db.Students.FindAsync(id);
            if (student == null) throw new KeyNotFoundException("Ученик не найден");

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Имя и фамилия обязательны");

            if (classId.HasValue && !await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            student.FirstName = firstName;
            student.LastName = lastName;
            student.BirthDate = birthDate;
            student.ClassId = classId ?? 0;

            await _db.SaveChangesAsync();
            return student;
        }

        /// <summary>
        /// Получает список учеников по классу.
        /// </summary>
        /// <param name="classId">ID класса</param>
        /// <returns>Список учеников</returns>
        public async Task<IEnumerable<object>> GetStudentsByClassAsync(int classId)
        {
            if (!await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            return await _db.Students
                .Where(s => s.ClassId == classId)
                .Select(s => new
                {
                    s.StudentId,
                    s.FirstName,
                    s.LastName,
                    s.BirthDate
                })
                .OrderBy(s => s.LastName)
                .ToListAsync();
        }
    }
}
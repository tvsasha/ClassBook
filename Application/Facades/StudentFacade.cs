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

        /// <summary>
        /// Получает ВСЕх учеников с информацией о классе (для админ-панели).
        /// </summary>
        /// <returns>Список всех учеников</returns>
        public async Task<IEnumerable<object>> GetAllStudentsAsync()
        {
            return await _db.Students
                .Include(s => s.Class)
                .Select(s => new
                {
                    s.StudentId,
                    s.FirstName,
                    s.LastName,
                    s.BirthDate,
                    s.ClassId,
                    Class = s.Class != null ? new { s.Class.ClassId, s.Class.Name } : null
                })
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();
        }

        public async Task DeleteStudentAsync(int studentId)
        {
            var student = await _db.Students
                .Include(s => s.Grades)
                .Include(s => s.Attendances)
                .Include(s => s.Parents)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (student.Grades != null && student.Grades.Any())
                _db.Grades.RemoveRange(student.Grades);

            if (student.Attendances != null && student.Attendances.Any())
                _db.Attendances.RemoveRange(student.Attendances);

            if (student.Parents != null && student.Parents.Any())
                _db.StudentParents.RemoveRange(student.Parents);

            _db.Students.Remove(student);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Привязывает студента к родителю.
        /// </summary>
        /// <param name="parentId">ID родителя (User с ролью Родитель)</param>
        /// <param name="studentId">ID студента</param>
        public async Task AttachStudentToParentAsync(int parentId, int studentId)
        {
            Console.WriteLine($"[AttachStudentToParentAsync] Попытка привязать студента {studentId} к родителю {parentId}");
            
            // Проверяем, что родитель существует и имеет роль "Родитель"
            var parent = await _db.Users.FirstOrDefaultAsync(u => u.Id == parentId);
            if (parent == null)
            {
                Console.WriteLine($"[AttachStudentToParentAsync] Родитель {parentId} не найден");
                throw new KeyNotFoundException("Родитель не найден");
            }

            Console.WriteLine($"[AttachStudentToParentAsync] Найден родитель: {parent.FullName} (RoleId: {parent.RoleId})");

            // Получаем ID роли "Родитель" (обычно это ID 4)
            var parentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Родитель");
            if (parentRole == null || parent.RoleId != parentRole.Id)
            {
                Console.WriteLine($"[AttachStudentToParentAsync] Пользователь {parentId} не является родителем (ожидалось RoleId {parentRole?.Id}, получено {parent.RoleId})");
                throw new ArgumentException("Пользователь не является родителем");
            }

            // Проверяем, что студент существует
            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
            {
                Console.WriteLine($"[AttachStudentToParentAsync] Студент {studentId} не найден");
                throw new KeyNotFoundException("Студент не найден");
            }

            Console.WriteLine($"[AttachStudentToParentAsync] Найден студент: {student.FirstName} {student.LastName}");

            // Проверяем, что связь еще не создана
            var existingLink = await _db.StudentParents
                .FirstOrDefaultAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId);
            if (existingLink != null)
            {
                Console.WriteLine($"[AttachStudentToParentAsync] Связь уже существует (StudentParentId: {existingLink.StudentParentId})");
                throw new ArgumentException("Этот студент уже привязан к этому родителю");
            }

            // Создаем связь
            var studentParent = new StudentParent
            {
                StudentId = studentId,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow
            };

            Console.WriteLine($"[AttachStudentToParentAsync] Создаем новую связь StudentParent");
            
            _db.StudentParents.Add(studentParent);
            await _db.SaveChangesAsync();
            
            Console.WriteLine($"[AttachStudentToParentAsync] ✓ Связь успешно создана и сохранена в БД");
        }
    }
}
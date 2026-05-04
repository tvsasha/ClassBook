using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    public class ParentFacade
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;

        public ParentFacade(AppDbContext db, IPasswordHasher hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        /// <summary>
        /// Создает учетную запись родителя сразу из контекста конкретного ученика
        /// и тут же привязывает ее к этому ученику.
        /// </summary>
        public async Task<User> CreateParentAccountForStudentAsync(int studentId, string fullName, string login, string password)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("ФИО, логин и временный пароль обязательны");

            var student = await _db.Students
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            var normalizedLogin = login.Trim();
            if (await _db.Users.AnyAsync(u => u.Login == normalizedLogin))
                throw new InvalidOperationException("Логин уже занят");

            var parentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Родитель");
            if (parentRole == null)
                throw new KeyNotFoundException("Роль 'Родитель' не найдена");

            var parent = new User
            {
                Login = normalizedLogin,
                FullName = fullName.Trim(),
                PasswordHash = _hasher.Hash(password),
                RoleId = parentRole.Id,
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(parent);
            await _db.SaveChangesAsync();

            var studentParent = new StudentParent
            {
                StudentId = studentId,
                ParentId = parent.Id,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudentParents.Add(studentParent);
            await _db.SaveChangesAsync();

            return parent;
        }

        /// <summary>
        /// Привязывает родителя к ученику
        /// </summary>
        public async Task<StudentParent> AddParentToStudentAsync(int studentId, int parentId)
        {
            // Проверяем, что студент существует
            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException($"Ученик с ID {studentId} не найден");

            // Проверяем, что родитель существует
            var parent = await _db.Users.FindAsync(parentId);
            if (parent == null)
                throw new KeyNotFoundException($"Родитель с ID {parentId} не найден");

            // Проверяем, что у родителя роль "Родитель"
            var parentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Родитель");
            if (parent.RoleId != parentRole?.Id)
                throw new InvalidOperationException("Пользователь не является родителем");

            // Проверяем, что связь не существует
            var existing = await _db.StudentParents
                .FirstOrDefaultAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId);
            if (existing != null)
                throw new InvalidOperationException("Связь ученик-родитель уже существует");

            var studentParent = new StudentParent
            {
                StudentId = studentId,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudentParents.Add(studentParent);
            await _db.SaveChangesAsync();

            return studentParent;
        }

        /// <summary>
        /// Получает всех учеников родителя
        /// </summary>
        public async Task<List<dynamic>> GetStudentsForParentAsync(int parentId)
        {
            Console.WriteLine($"[ParentFacade.GetStudentsForParentAsync] parentId={parentId}");
            try
            {
                var studentParents = await _db.StudentParents
                    .Where(sp => sp.ParentId == parentId)
                    .Include(sp => sp.Student)
                    .ThenInclude(s => s.Class)
                    .ToListAsync();

                Console.WriteLine($"[ParentFacade.GetStudentsForParentAsync] Found {studentParents.Count} StudentParent records");

                var students = studentParents
                    .Select(sp => new
                    {
                        sp.Student.StudentId,
                        sp.Student.FirstName,
                        sp.Student.LastName,
                        sp.Student.BirthDate,
                        @class = new { name = sp.Student.Class?.Name ?? "Класс не определен" }
                    })
                    .Cast<dynamic>()
                    .ToList();

                Console.WriteLine($"[ParentFacade.GetStudentsForParentAsync] Returning {students.Count} students");
                
                return students;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParentFacade.GetStudentsForParentAsync] Exception: {ex.Message}");
                Console.WriteLine($"[ParentFacade.GetStudentsForParentAsync] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Получает всех родителей ученика
        /// </summary>
        public async Task<List<User>> GetParentsForStudentAsync(int studentId)
        {
            return await _db.StudentParents
                .Where(sp => sp.StudentId == studentId)
                .Include(sp => sp.Parent)
                .Select(sp => sp.Parent)
                .ToListAsync();
        }

        /// <summary>
        /// Удаляет связь ученик-родитель
        /// </summary>
        public async Task RemoveParentFromStudentAsync(int studentId, int parentId)
        {
            var studentParent = await _db.StudentParents
                .FirstOrDefaultAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId);

            if (studentParent == null)
                throw new KeyNotFoundException("Связь ученик-родитель не найдена");

            _db.StudentParents.Remove(studentParent);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Получает количество учеников у родителя
        /// </summary>
        public async Task<int> GetStudentCountForParentAsync(int parentId)
        {
            return await _db.StudentParents
                .CountAsync(sp => sp.ParentId == parentId);
        }

        /// <summary>
        /// Получает количество родителей у ученика
        /// </summary>
        public async Task<int> GetParentCountForStudentAsync(int studentId)
        {
            return await _db.StudentParents
                .CountAsync(sp => sp.StudentId == studentId);
        }

        /// <summary>
        /// Проверяет, является ли пользователь родителем конкретного ученика
        /// </summary>
        public async Task<bool> IsParentOfStudentAsync(int parentId, int studentId)
        {
            return await _db.StudentParents
                .AnyAsync(sp => sp.ParentId == parentId && sp.StudentId == studentId);
        }

        /// <summary>
        /// Получает детальную информацию о связи ученик-родитель с полными данными
        /// </summary>
        public async Task<dynamic?> GetStudentParentDetailAsync(int studentId, int parentId)
        {
            var studentParent = await _db.StudentParents
                .Where(sp => sp.StudentId == studentId && sp.ParentId == parentId)
                .Include(sp => sp.Student)
                .ThenInclude(s => s.Class)
                .Include(sp => sp.Parent)
                .FirstOrDefaultAsync();

            if (studentParent == null)
                return null;

            return new
            {
                studentParent.StudentParentId,
                Student = new
                {
                    studentParent.Student.StudentId,
                    studentParent.Student.FirstName,
                    studentParent.Student.LastName,
                    Class = studentParent.Student.Class.Name
                },
                Parent = new
                {
                    studentParent.Parent.Id,
                    studentParent.Parent.FullName,
                    studentParent.Parent.Login
                },
                studentParent.CreatedAt
            };
        }
    }
}

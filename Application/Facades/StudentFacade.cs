using ClassBook.Application.DTOs;
using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
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
        private readonly IPasswordHasher _hasher;

        public StudentFacade(AppDbContext db, IPasswordHasher hasher)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        }

        /// <summary>
        /// Создаёт новую карточку ученика без учетной записи.
        /// </summary>
        public async Task<AdminStudentDto> CreateStudentAsync(string firstName, string lastName, DateTime birthDate, int? classId = null)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Имя и фамилия обязательны");

            if (classId.HasValue && !await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            var student = new Student
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                BirthDate = birthDate,
                ClassId = classId ?? 0
            };

            _db.Students.Add(student);
            await _db.SaveChangesAsync();
            return MapStudent(student, null);
        }

        public async Task<AdminStudentDto> UpdateStudentAsync(int id, string firstName, string lastName, DateTime birthDate, int? classId)
        {
            var student = await _db.Students.FindAsync(id);
            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Имя и фамилия обязательны");

            if (classId.HasValue && !await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            student.FirstName = firstName.Trim();
            student.LastName = lastName.Trim();
            student.BirthDate = birthDate;
            student.ClassId = classId ?? 0;

            await _db.SaveChangesAsync();

            string? className = null;
            if (student.ClassId > 0)
            {
                className = await _db.Classes
                    .Where(c => c.ClassId == student.ClassId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();
            }

            return MapStudent(student, className);
        }

        /// <summary>
        /// Создает учетную запись для существующей карточки ученика.
        /// </summary>
        public async Task<IssuedStudentAccountDto> CreateStudentAccountAsync(int studentId, string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Логин и временный пароль обязательны");

            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (student.UserId.HasValue)
                throw new ArgumentException("Для этого ученика уже создана учетная запись");

            var normalizedLogin = login.Trim();
            if (await _db.Users.AnyAsync(u => u.Login == normalizedLogin))
                throw new ArgumentException("Логин уже занят");

            var studentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Ученик");
            if (studentRole == null)
                throw new KeyNotFoundException("Роль 'Ученик' не найдена");

            var user = new User
            {
                Login = normalizedLogin,
                FullName = $"{student.LastName} {student.FirstName}".Trim(),
                PasswordHash = _hasher.Hash(password),
                RoleId = studentRole.Id,
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            student.UserId = user.Id;
            await _db.SaveChangesAsync();

            return new IssuedStudentAccountDto
            {
                Id = user.Id,
                Login = user.Login,
                FullName = user.FullName,
                MustChangePassword = user.MustChangePassword,
                Message = "Учетная запись ученика создана"
            };
        }

        /// <summary>
        /// Получает список учеников по классу.
        /// </summary>
        public async Task<IEnumerable<AdminStudentDto>> GetStudentsByClassAsync(int classId)
        {
            if (!await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            return await _db.Students
                .Where(s => s.ClassId == classId)
                .Select(s => new AdminStudentDto
                {
                    StudentId = s.StudentId,
                    UserId = s.UserId,
                    HasAccount = s.UserId.HasValue,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    BirthDate = s.BirthDate,
                    ClassId = s.ClassId
                })
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();
        }

        /// <summary>
        /// Получает всех учеников с информацией о классе.
        /// </summary>
        public async Task<IEnumerable<AdminStudentDto>> GetAllStudentsAsync()
        {
            return await _db.Students
                .Include(s => s.Class)
                .Select(s => new AdminStudentDto
                {
                    StudentId = s.StudentId,
                    UserId = s.UserId,
                    HasAccount = s.UserId.HasValue,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    BirthDate = s.BirthDate,
                    ClassId = s.ClassId,
                    ClassName = s.Class != null ? s.Class.Name : null
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
        /// Привязывает ученика к родителю.
        /// </summary>
        public async Task AttachStudentToParentAsync(int parentId, int studentId)
        {
            var parent = await _db.Users.FirstOrDefaultAsync(u => u.Id == parentId);
            if (parent == null)
                throw new KeyNotFoundException("Родитель не найден");

            var parentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Родитель");
            if (parentRole == null || parent.RoleId != parentRole.Id)
                throw new ArgumentException("Пользователь не является родителем");

            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException("Студент не найден");

            var existingLink = await _db.StudentParents
                .FirstOrDefaultAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId);

            if (existingLink != null)
                throw new ArgumentException("Этот студент уже привязан к этому родителю");

            var studentParent = new StudentParent
            {
                StudentId = studentId,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudentParents.Add(studentParent);
            await _db.SaveChangesAsync();
        }

        private static AdminStudentDto MapStudent(Student student, string? className)
        {
            return new AdminStudentDto
            {
                StudentId = student.StudentId,
                UserId = student.UserId,
                HasAccount = student.UserId.HasValue,
                FirstName = student.FirstName,
                LastName = student.LastName,
                BirthDate = student.BirthDate,
                ClassId = student.ClassId,
                ClassName = className
            };
        }
    }
}

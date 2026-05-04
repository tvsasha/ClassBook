// Application/Facades/SubjectFacade.cs
using ClassBook.Application.DTOs;
using ClassBook.Domain.Entities;
using ClassBook.Domain.Constants;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для управления предметами и привязкой учителей.
    /// </summary>
    public class SubjectFacade
    {
        private readonly AppDbContext _db;

        public SubjectFacade(AppDbContext db) => _db = db;

        public async Task<IEnumerable<SubjectLookupDto>> GetSubjectsForTeacherAsync(int teacherId)
        {
            return await _db.Subjects
                .Where(s => s.TeacherId == teacherId)
                .Select(s => new SubjectLookupDto
                {
                    SubjectId = s.SubjectId,
                    Name = s.Name
                })
                .ToListAsync();
        }
        /// <summary>
        /// Создаёт новый предмет.
        /// </summary>
        public async Task<Subject> CreateSubjectAsync(string name, int teacherId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название предмета обязательно");

            var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (teacher == null)
                throw new InvalidOperationException("Учитель не найден");

            var subject = new Subject { Name = name, TeacherId = teacherId };
            _db.Subjects.Add(subject);
            await _db.SaveChangesAsync();
            return subject;
        }

        public async Task<IEnumerable<SubjectClassAssignmentDto>> GetClassesForSubjectAsync(int subjectId)
        {
            return await _db.Lessons
                .Where(l => l.SubjectId == subjectId)
                .Include(l => l.Class)
                .Include(l => l.Teacher)
                .Select(l => new SubjectClassAssignmentDto
                {
                    ClassId = l.ClassId,
                    ClassName = l.Class.Name,
                    TeacherId = l.TeacherId,
                    TeacherName = l.Teacher.FullName ?? "Не назначен"
                })
                .Distinct()
                .ToListAsync();
        }

        /// <summary>
        /// Прикрепляет учителя к предмету.
        /// </summary>
        public async Task AttachTeacherToSubjectAsync(int subjectId, int teacherId)
        {
            var subject = await _db.Subjects.FindAsync(subjectId);
            if (subject == null) throw new KeyNotFoundException("Предмет не найден");

            var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (teacher == null) throw new InvalidOperationException("Учитель не найден");

            subject.TeacherId = teacherId;
            await _db.SaveChangesAsync();
        }
    }
}

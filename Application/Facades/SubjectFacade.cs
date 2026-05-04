// Application/Facades/SubjectFacade.cs
using ClassBook.Application.DTOs;
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
        public async Task<IEnumerable<SubjectAdminListItemDto>> GetAllSubjectsAsync()
        {
            return await _db.Subjects
                .Include(s => s.Teacher)
                .OrderBy(s => s.Name)
                .Select(s => new SubjectAdminListItemDto
                {
                    SubjectId = s.SubjectId,
                    Name = s.Name,
                    TeacherId = s.TeacherId,
                    TeacherName = s.Teacher != null ? s.Teacher.FullName : "Не назначен"
                })
                .ToListAsync();
        }

        /// <summary>
        /// Создаёт новый предмет.
        /// </summary>
        public async Task<SubjectAdminResponseDto> CreateSubjectAsync(string name, int teacherId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название предмета обязательно");

            var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (teacher == null)
                throw new InvalidOperationException("Учитель не найден или это не учитель");

            var subject = new Domain.Entities.Subject { Name = name.Trim(), TeacherId = teacherId };
            _db.Subjects.Add(subject);
            await _db.SaveChangesAsync();
            return new SubjectAdminResponseDto
            {
                SubjectId = subject.SubjectId,
                Name = subject.Name,
                TeacherName = teacher.FullName
            };
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
        public async Task<SubjectTeacherAttachResultDto> AttachTeacherToSubjectAsync(int subjectId, int teacherId)
        {
            var subject = await _db.Subjects.FindAsync(subjectId);
            if (subject == null) throw new KeyNotFoundException("Предмет не найден");

            var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (teacher == null) throw new InvalidOperationException("Учитель не найден или это не учитель");

            subject.TeacherId = teacherId;
            await _db.SaveChangesAsync();

            return new SubjectTeacherAttachResultDto
            {
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName,
                Message = $"Учитель успешно прикреплён: {teacher.FullName}"
            };
        }

        public async Task<SubjectAdminResponseDto> UpdateSubjectAsync(int subjectId, string name, int teacherId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название предмета обязательно");

            var subject = await _db.Subjects.FindAsync(subjectId);
            if (subject == null)
                throw new KeyNotFoundException("Предмет не найден");

            var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (teacher == null)
                throw new InvalidOperationException("Учитель не найден или это не учитель");

            subject.Name = name.Trim();
            subject.TeacherId = teacherId;
            await _db.SaveChangesAsync();

            return new SubjectAdminResponseDto
            {
                SubjectId = subject.SubjectId,
                Name = subject.Name,
                TeacherName = teacher.FullName
            };
        }

        public async Task DeleteSubjectAsync(int subjectId)
        {
            var subject = await _db.Subjects.FindAsync(subjectId);
            if (subject == null)
                throw new KeyNotFoundException("Предмет не найден");

            if (await _db.Lessons.AnyAsync(l => l.SubjectId == subjectId))
                throw new InvalidOperationException("Нельзя удалить предмет, к которому привязаны уроки");

            _db.Subjects.Remove(subject);
            await _db.SaveChangesAsync();
        }
    }
}

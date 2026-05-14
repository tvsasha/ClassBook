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
            var subjects = await _db.Subjects
                .Where(s => s.TeacherId == teacherId || s.ClassAssignments!.Any(assignment => assignment.TeacherId == teacherId))
                .Include(s => s.ClassAssignments!)
                .ThenInclude(assignment => assignment.Class)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return subjects.Select(s => new SubjectLookupDto
            {
                SubjectId = s.SubjectId,
                Name = s.Name,
                ClassIds = (s.ClassAssignments ?? [])
                    .Where(assignment => assignment.TeacherId == teacherId)
                    .Select(assignment => assignment.ClassId)
                    .Distinct()
                    .ToList(),
                Classes = string.Join(", ", (s.ClassAssignments ?? [])
                    .Where(assignment => assignment.TeacherId == teacherId)
                    .Select(assignment => assignment.Class.Name)
                    .Distinct())
            });
        }
        public async Task<IEnumerable<SubjectAdminListItemDto>> GetAllSubjectsAsync()
        {
            var subjects = await _db.Subjects
                .Include(s => s.Teacher)
                .Include(s => s.ClassAssignments!)
                .ThenInclude(assignment => assignment.Class)
                .Include(s => s.ClassAssignments!)
                .ThenInclude(assignment => assignment.Teacher)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return subjects.Select(s => new SubjectAdminListItemDto
            {
                SubjectId = s.SubjectId,
                Name = s.Name,
                TeacherId = s.TeacherId,
                TeacherName = s.Teacher != null ? s.Teacher.FullName : "Не назначен",
                ClassAssignments = (s.ClassAssignments ?? [])
                    .OrderBy(assignment => assignment.Class.Name)
                    .Select(assignment => new SubjectClassAssignmentDto
                    {
                        ClassId = assignment.ClassId,
                        ClassName = assignment.Class.Name,
                        TeacherId = assignment.TeacherId,
                        TeacherName = assignment.Teacher.FullName
                    })
                    .ToList()
            });
        }

        /// <summary>
        /// Создаёт новый предмет.
        /// </summary>
        public async Task<SubjectAdminResponseDto> CreateSubjectAsync(string name, int teacherId, int? classId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название предмета обязательно");

            var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (teacher == null)
                throw new InvalidOperationException("Учитель не найден или это не учитель");

            var subject = new Domain.Entities.Subject { Name = name.Trim(), TeacherId = teacherId };
            _db.Subjects.Add(subject);
            if (classId.HasValue)
            {
                await ValidateClassAsync(classId.Value);
                _db.SubjectClassAssignments.Add(new Domain.Entities.SubjectClassAssignment
                {
                    Subject = subject,
                    ClassId = classId.Value,
                    TeacherId = teacherId,
                    CreatedAt = DateTime.UtcNow
                });
            }

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
            if (!await _db.Subjects.AnyAsync(s => s.SubjectId == subjectId))
                throw new KeyNotFoundException("Предмет не найден");

            return await _db.SubjectClassAssignments
                .Where(assignment => assignment.SubjectId == subjectId)
                .Include(assignment => assignment.Class)
                .Include(assignment => assignment.Teacher)
                .OrderBy(assignment => assignment.Class.Name)
                .Select(assignment => new SubjectClassAssignmentDto
                {
                    ClassId = assignment.ClassId,
                    ClassName = assignment.Class.Name,
                    TeacherId = assignment.TeacherId,
                    TeacherName = assignment.Teacher.FullName
                })
                .ToListAsync();
        }

        public async Task<SubjectClassAssignmentDto> AssignSubjectToClassAsync(int subjectId, int classId, int teacherId)
        {
            var subject = await _db.Subjects.FindAsync(subjectId);
            if (subject == null)
                throw new KeyNotFoundException("Предмет не найден");

            var teacher = await ValidateTeacherAsync(teacherId);
            var classEntity = await ValidateClassAsync(classId);

            var existing = await _db.SubjectClassAssignments
                .FirstOrDefaultAsync(assignment =>
                    assignment.SubjectId == subjectId &&
                    assignment.ClassId == classId &&
                    assignment.TeacherId == teacherId);
            if (existing == null)
            {
                _db.SubjectClassAssignments.Add(new Domain.Entities.SubjectClassAssignment
                {
                    SubjectId = subjectId,
                    ClassId = classId,
                    TeacherId = teacherId,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            return new SubjectClassAssignmentDto
            {
                ClassId = classId,
                ClassName = classEntity.Name,
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName
            };
        }

        public async Task RemoveSubjectClassAssignmentAsync(int subjectId, int classId, int teacherId)
        {
            var assignment = await _db.SubjectClassAssignments
                .FirstOrDefaultAsync(item =>
                    item.SubjectId == subjectId &&
                    item.ClassId == classId &&
                    item.TeacherId == teacherId);
            if (assignment == null)
                throw new KeyNotFoundException("Назначение предмета не найдено");

            var hasLessons = await _db.Lessons.AnyAsync(lesson =>
                lesson.SubjectId == subjectId &&
                lesson.ClassId == classId &&
                lesson.TeacherId == teacherId);
            if (hasLessons)
                throw new InvalidOperationException("Нельзя снять назначение, по которому уже созданы уроки");

            _db.SubjectClassAssignments.Remove(assignment);
            await _db.SaveChangesAsync();
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

        private async Task<Domain.Entities.User> ValidateTeacherAsync(int teacherId)
        {
            var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (teacher == null)
                throw new InvalidOperationException("Учитель не найден или это не учитель");

            return teacher;
        }

        private async Task<Domain.Entities.Class> ValidateClassAsync(int classId)
        {
            var classEntity = await _db.Classes.FirstOrDefaultAsync(c => c.ClassId == classId);
            if (classEntity == null)
                throw new KeyNotFoundException("Класс не найден");

            return classEntity;
        }
    }
}

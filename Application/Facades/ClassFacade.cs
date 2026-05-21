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
                .AsNoTracking()
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

        public async Task DeleteClassAsync(int classId, string studentAction = "keepWithoutClass", int? targetClassId = null)
        {
            var classEntity = await _db.Classes.FindAsync(classId);
            if (classEntity == null)
                throw new KeyNotFoundException("Класс не найден");

            if (targetClassId == classId)
                throw new InvalidOperationException("Нельзя перевести учеников в удаляемый класс");

            if (targetClassId.HasValue && !await _db.Classes.AnyAsync(c => c.ClassId == targetClassId.Value))
                throw new KeyNotFoundException("Класс для перевода не найден");

            if (await _db.Lessons.AnyAsync(l => l.ClassId == classId))
            {
                throw new InvalidOperationException("Нельзя удалить класс, по которому уже созданы уроки. Сначала перенесите или удалите уроки в расписании.");
            }

            var students = await _db.Students
                .Where(s => s.ClassId == classId)
                .ToListAsync();

            switch ((studentAction ?? "keepWithoutClass").Trim())
            {
                case "deleteStudents":
                    var studentIds = students.Select(student => student.StudentId).ToList();
                    if (studentIds.Count > 0)
                    {
                        var grades = await _db.Grades
                            .Where(grade => studentIds.Contains(grade.StudentId))
                            .ToListAsync();
                        var attendances = await _db.Attendances
                            .Where(attendance => studentIds.Contains(attendance.StudentId))
                            .ToListAsync();
                        var parentLinks = await _db.StudentParents
                            .Where(link => studentIds.Contains(link.StudentId))
                            .ToListAsync();

                        _db.Grades.RemoveRange(grades);
                        _db.Attendances.RemoveRange(attendances);
                        _db.StudentParents.RemoveRange(parentLinks);
                    }
                    _db.Students.RemoveRange(students);
                    break;
                case "moveStudents":
                    if (!targetClassId.HasValue)
                        throw new InvalidOperationException("Выберите класс, в который нужно перевести учеников");
                    foreach (var student in students)
                    {
                        student.ClassId = targetClassId.Value;
                    }
                    break;
                case "keepWithoutClass":
                    foreach (var student in students)
                    {
                        student.ClassId = null;
                    }
                    break;
                default:
                    throw new InvalidOperationException("Неизвестное действие с учениками");
            }

            var classTeachers = await _db.ClassTeachers
                .Where(assignment => assignment.ClassId == classId)
                .ToListAsync();
            var subjectAssignments = await _db.SubjectClassAssignments
                .Where(assignment => assignment.ClassId == classId)
                .ToListAsync();

            _db.ClassTeachers.RemoveRange(classTeachers);
            _db.SubjectClassAssignments.RemoveRange(subjectAssignments);
            _db.Classes.Remove(classEntity);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<ClassListItemDto>> GetClassesForTeacherAsync(int teacherId)
        {
            var lessonClasses = _db.Lessons
                .Where(l => l.TeacherId == teacherId)
                .Select(l => l.Class);
            var assignedClasses = _db.SubjectClassAssignments
                .Where(a => a.TeacherId == teacherId)
                .Select(a => a.Class);

            return await lessonClasses
                .Union(assignedClasses)
                .Distinct()
                .Select(c => new ClassListItemDto
                {
                    ClassId = c.ClassId,
                    Name = c.Name
                })
                .OrderBy(c => c.Name)
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

            student.ClassId = null;
            await _db.SaveChangesAsync();
        }
    }
}

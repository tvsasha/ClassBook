using ClassBook.Infrastructure.Data;
using ClassBook.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Application.Facades;

public sealed class SchoolAccessFacade
{
    private readonly AppDbContext _db;

    public SchoolAccessFacade(AppDbContext db) => _db = db;

    public static void EnsureTeacherIdentity(int currentUserId, string role, int requestedTeacherId)
    {
        if (IsPrivileged(role) || currentUserId == requestedTeacherId)
            return;
        throw new UnauthorizedAccessException("Нет доступа к данным другого преподавателя");
    }

    public async Task EnsureClassAccessAsync(int currentUserId, string role, int classId)
    {
        if (IsPrivileged(role)) return;
        var allowed = await _db.SubjectClassAssignments.AnyAsync(x => x.TeacherId == currentUserId && x.ClassId == classId)
            || await _db.ClassTeachers.AnyAsync(x => x.TeacherId == currentUserId && x.ClassId == classId)
            || await _db.Lessons.AnyAsync(x => x.TeacherId == currentUserId && x.ClassId == classId);
        if (!allowed) throw new UnauthorizedAccessException("Нет доступа к выбранному классу");
    }

    public async Task EnsureClassReadAccessAsync(int currentUserId, int classId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserId && x.IsActive)
            .Select(x => new { x.RoleId })
            .SingleOrDefaultAsync();

        if (user is null)
            throw new UnauthorizedAccessException("Учётная запись недоступна");

        if (user.RoleId is SystemRoleIds.Administrator or SystemRoleIds.Director)
            return;

        var allowed = user.RoleId switch
        {
            SystemRoleIds.Teacher =>
                await _db.SubjectClassAssignments.AnyAsync(x => x.TeacherId == currentUserId && x.ClassId == classId)
                || await _db.ClassTeachers.AnyAsync(x => x.TeacherId == currentUserId && x.ClassId == classId)
                || await _db.Lessons.AnyAsync(x => x.TeacherId == currentUserId && x.ClassId == classId),
            SystemRoleIds.Student =>
                await _db.Students.AnyAsync(x => x.UserId == currentUserId && x.ClassId == classId),
            SystemRoleIds.Parent =>
                await _db.StudentParents.AnyAsync(x => x.ParentId == currentUserId && x.Student.ClassId == classId),
            _ => false
        };

        if (!allowed)
            throw new UnauthorizedAccessException("Нет доступа к расписанию выбранного класса");
    }

    public async Task EnsureLessonAccessAsync(int currentUserId, string role, int lessonId, bool writeAccess)
    {
        if (role == "Администратор" || (!writeAccess && role == "Директор")) return;
        var allowed = role == "Учитель" && await _db.Lessons.AnyAsync(x => x.LessonId == lessonId && x.TeacherId == currentUserId);
        if (!allowed) throw new UnauthorizedAccessException("Нет доступа к выбранному уроку");
    }

    public async Task EnsureGradeWriteAccessAsync(int currentUserId, string role, int gradeId)
    {
        if (role == "Администратор") return;
        var allowed = role == "Учитель" && await _db.Grades.AnyAsync(x => x.GradeId == gradeId && x.Lesson.TeacherId == currentUserId);
        if (!allowed) throw new UnauthorizedAccessException("Нет доступа к выбранной оценке");
    }

    private static bool IsPrivileged(string role) => role is "Администратор" or "Директор";
}

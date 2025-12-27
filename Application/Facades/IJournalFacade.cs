using ClassBook.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Интерфейс единого фасада для бизнес-логики приложения ClassBook.
    /// Предоставляет унифицированный доступ ко всем основным операциям системы.
    /// </summary>
    public interface IJournalFacade
    {
        // ── Аутентификация и регистрация ───────────────────────────────────────────────────────────────
        /// <summary>
        /// Регистрирует нового пользователя (доступно только администратору).
        /// </summary>
        /// <param name="login">Логин пользователя</param>
        /// <param name="fullName">Полное имя пользователя</param>
        /// <param name="password">Пароль пользователя</param>
        /// <param name="roleId">Идентификатор роли (1=Администратор, 2=Учитель)</param>
        /// <returns>Созданный пользователь</returns>
        Task<User> RegisterAsync(string login, string fullName, string password, int roleId);

        /// <summary>
        /// Выполняет вход пользователя в систему.
        /// </summary>
        /// <param name="login">Логин пользователя</param>
        /// <param name="password">Пароль пользователя</param>
        /// <returns>Пользователь, если авторизация успешна; иначе null</returns>
        Task<User?> LoginAsync(string login, string password);

        // ── Управление пользователями ──────────────────────────────────────────────────────────────────
        /// <summary>
        /// Получает список всех пользователей.
        /// </summary>
        /// <returns>Список пользователей с ролями</returns>
        Task<IEnumerable<object>> GetAllUsersAsync();

        /// <summary>
        /// Получает список учителей.
        /// </summary>
        /// <returns>Список учителей</returns>
        Task<IEnumerable<object>> GetTeachersAsync();

        /// <summary>
        /// Получает пользователя по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор пользователя</param>
        /// <returns>Пользователь с ролью или null</returns>
        Task<object?> GetUserByIdAsync(int id);

        /// <summary>
        /// Обновляет данные пользователя.
        /// </summary>
        /// <param name="id">Идентификатор пользователя</param>
        /// <param name="login">Новый логин (опционально)</param>
        /// <param name="fullName">Новое полное имя (опционально)</param>
        /// <param name="password">Новый пароль (опционально)</param>
        /// <param name="roleId">Новая роль (опционально)</param>
        /// <param name="isActive">Новый статус активности (опционально)</param>
        Task UpdateUserAsync(int id, string? login = null, string? fullName = null,
                             string? password = null, int? roleId = null, bool? isActive = null);

        /// <summary>
        /// Удаляет пользователя.
        /// </summary>
        /// <param name="id">Идентификатор пользователя</param>
        Task DeleteUserAsync(int id);

        // ── Управление классами ────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Создаёт новый класс.
        /// </summary>
        /// <param name="name">Название класса</param>
        /// <returns>Созданный класс</returns>
        Task<Class> CreateClassAsync(string name);

        /// <summary>
        /// Назначает ученика в класс.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика</param>
        /// <param name="classId">Идентификатор класса</param>
        Task AssignStudentToClassAsync(int studentId, int classId);

        /// <summary>
        /// Удаляет ученика из класса.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика</param>
        Task RemoveStudentFromClassAsync(int studentId);

        // ── Управление предметами ──────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Создаёт новый предмет.
        /// </summary>
        /// <param name="name">Название предмета</param>
        /// <param name="teacherId">Идентификатор учителя</param>
        /// <returns>Созданный предмет</returns>
        Task<Subject> CreateSubjectAsync(string name, int teacherId);

        /// <summary>
        /// Прикрепляет учителя к предмету.
        /// </summary>
        /// <param name="subjectId">Идентификатор предмета</param>
        /// <param name="teacherId">Идентификатор учителя</param>
        Task AttachTeacherToSubjectAsync(int subjectId, int teacherId);

        // ── Управление ролями ──────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Получает список ролей.
        /// </summary>
        /// <returns>Список ролей</returns>
        Task<IEnumerable<object>> GetRolesAsync();

        // ── Управление уроками ─────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Создаёт новый урок.
        /// </summary>
        /// <param name="subjectId">Идентификатор предмета</param>
        /// <param name="classId">Идентификатор класса</param>
        /// <param name="teacherId">Идентификатор учителя</param>
        /// <param name="topic">Тема урока</param>
        /// <param name="date">Дата урока</param>
        /// <param name="homework">Домашнее задание (опционально)</param>
        /// <returns>Созданный урок</returns>
        Task<Lesson> CreateLessonAsync(int subjectId, int classId, int teacherId, string topic, DateTime date, string? homework = null);

        // ── Управление оценками ────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Добавляет оценку ученику за урок.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока</param>
        /// <param name="studentId">Идентификатор ученика</param>
        /// <param name="value">Оценка (1-5)</param>
        /// <returns>Созданная оценка</returns>
        Task<Grade> AddGradeAsync(int lessonId, int studentId, int value);

        /// <summary>
        /// Получает оценки за урок.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока</param>
        /// <returns>Список оценок</returns>
        Task<IEnumerable<object>> GetGradesForLessonAsync(int lessonId);

        // ── Управление посещаемостью ───────────────────────────────────────────────────────────────────
        /// <summary>
        /// Отмечает посещаемость ученика на уроке.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока</param>
        /// <param name="studentId">Идентификатор ученика</param>
        /// <param name="status">Статус посещаемости (0 - отсутствовал, 1 - присутствовал и т.д.)</param>
        Task MarkAttendanceAsync(int lessonId, int studentId, byte status);

        /// <summary>
        /// Получает посещаемость за урок.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока</param>
        /// <returns>Список отметок посещаемости</returns>
        Task<IEnumerable<object>> GetAttendanceForLessonAsync(int lessonId);
    }
}
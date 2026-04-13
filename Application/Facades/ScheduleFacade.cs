using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    public class ScheduleFacade
    {
        private readonly AppDbContext _db;

        public ScheduleFacade(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Получает все фиксированные слоты расписания
        /// </summary>
        public async Task<List<Schedule>> GetAllScheduleSlotsAsync()
        {
            return await _db.Schedules
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.LessonNumber)
                .ToListAsync();
        }

        /// <summary>
        /// Получает расписание на конкретный день недели
        /// </summary>
        public async Task<List<Schedule>> GetScheduleByDayAsync(int dayOfWeek)
        {
            if (dayOfWeek < 0 || dayOfWeek > 4)
                throw new ArgumentException("День недели должен быть от 0 (Пн) до 4 (Пт)");

            return await _db.Schedules
                .Where(s => s.DayOfWeek == dayOfWeek)
                .OrderBy(s => s.LessonNumber)
                .ToListAsync();
        }

        /// <summary>
        /// Получает полное расписание на неделю (Пн-Пт)
        /// </summary>
        public async Task<Dictionary<int, List<Schedule>>> GetFullWeekScheduleAsync()
        {
            var schedules = await _db.Schedules
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.LessonNumber)
                .ToListAsync();

            var week = new Dictionary<int, List<Schedule>>();
            for (int day = 0; day < 5; day++)
            {
                week[day] = schedules.Where(s => s.DayOfWeek == day).ToList();
            }

            return week;
        }

        /// <summary>
        /// Получает расписание класса на определенную дату
        /// </summary>
        public async Task<List<(Schedule Schedule, Lesson? Lesson)>> GetScheduleByClassAsync(int classId, DateTime? date = null)
        {
            var dayOfWeek = (int)(date?.DayOfWeek ?? DateTime.UtcNow.DayOfWeek);
            // Преобразуем в нашу систему (0=Пн, 1=Вт... 4=Пт) - на случай если это будний день
            if (dayOfWeek == 0) dayOfWeek = 4; // Воскресенье -> обработка не нужна
            if (dayOfWeek > 5) dayOfWeek = dayOfWeek - 1;

            var schedules = await _db.Schedules
                .Where(s => s.DayOfWeek == dayOfWeek)
                .OrderBy(s => s.LessonNumber)
                .ToListAsync();

            var result = new List<(Schedule, Lesson?)>();
            foreach (var schedule in schedules)
            {
                var lesson = await _db.Lessons
                    .Where(l => l.ClassId == classId && l.ScheduleId == schedule.ScheduleId &&
                               (date == null || l.Date.Date == date.Value.Date))
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .FirstOrDefaultAsync();

                result.Add((schedule, lesson));
            }

            return result;
        }

        /// <summary>
        /// Создает новый слот расписания
        /// </summary>
        public async Task<Schedule> CreateScheduleSlotAsync(int dayOfWeek, int lessonNumber, TimeSpan startTime, TimeSpan endTime)
        {
            if (dayOfWeek < 0 || dayOfWeek > 4)
                throw new ArgumentException("День недели должен быть от 0 (Пн) до 4 (Пт)");
            if (lessonNumber < 1 || lessonNumber > 10)
                throw new ArgumentException("Номер урока должен быть от 1 до 10");

            // Проверяем уникальность
            var existing = await _db.Schedules
                .FirstOrDefaultAsync(s => s.DayOfWeek == dayOfWeek && s.LessonNumber == lessonNumber);
            if (existing != null)
                throw new InvalidOperationException($"Слот расписания для {dayOfWeek} дня, урока {lessonNumber} уже существует");

            var schedule = new Schedule
            {
                DayOfWeek = dayOfWeek,
                LessonNumber = lessonNumber,
                StartTime = startTime,
                EndTime = endTime,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Schedules.Add(schedule);
            await _db.SaveChangesAsync();

            return schedule;
        }

        /// <summary>
        /// Обновляет слот расписания
        /// </summary>
        public async Task<Schedule> UpdateScheduleSlotAsync(int scheduleId, TimeSpan startTime, TimeSpan endTime)
        {
            var schedule = await _db.Schedules.FindAsync(scheduleId);
            if (schedule == null)
                throw new KeyNotFoundException($"Слот расписания с ID {scheduleId} не найден");

            schedule.StartTime = startTime;
            schedule.EndTime = endTime;
            schedule.UpdatedAt = DateTime.UtcNow;

            _db.Schedules.Update(schedule);
            await _db.SaveChangesAsync();

            return schedule;
        }

        /// <summary>
        /// Удаляет слот расписания
        /// </summary>
        public async Task DeleteScheduleSlotAsync(int scheduleId)
        {
            var schedule = await _db.Schedules.FindAsync(scheduleId);
            if (schedule == null)
                throw new KeyNotFoundException($"Слот расписания с ID {scheduleId} не найден");

            _db.Schedules.Remove(schedule);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Получает слот расписания по ID
        /// </summary>
        public async Task<Schedule?> GetScheduleSlotAsync(int scheduleId)
        {
            return await _db.Schedules.FindAsync(scheduleId);
        }
    }
}

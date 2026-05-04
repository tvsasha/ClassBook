using ClassBook.Application.DTOs;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    public class AuditFacade
    {
        private readonly AppDbContext _db;

        public AuditFacade(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Логирует действие без полезной нагрузки изменений.
        /// </summary>
        public Task LogActionAsync(int userId, string entityType, int entityId, string action)
        {
            return LogActionCoreAsync(userId, entityType, entityId, action, null, null);
        }

        /// <summary>
        /// Логирует действие, у которого старые и новые значения имеют один DTO-тип.
        /// </summary>
        public Task LogActionAsync<TAuditValues>(
            int userId,
            string entityType,
            int entityId,
            string action,
            TAuditValues? oldValues = default,
            TAuditValues? newValues = default)
            where TAuditValues : class
        {
            return LogActionCoreAsync(userId, entityType, entityId, action, oldValues, newValues);
        }

        /// <summary>
        /// Логирует действие, у которого старые и новые значения представлены разными DTO-типами.
        /// </summary>
        public Task LogActionAsync<TOldValues, TNewValues>(
            int userId,
            string entityType,
            int entityId,
            string action,
            TOldValues? oldValues,
            TNewValues? newValues)
            where TOldValues : class
            where TNewValues : class
        {
            return LogActionCoreAsync(userId, entityType, entityId, action, oldValues, newValues);
        }

        private async Task LogActionCoreAsync(int userId, string entityType, int entityId, string action, object? oldValues, object? newValues)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                Timestamp = DateTime.UtcNow
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Получает все логи аудита для конкретной сущности
        /// </summary>
        public async Task<List<AuditLog>> GetAuditLogAsync(int entityId, string entityType)
        {
            return await _db.AuditLogs
                .Where(al => al.EntityId == entityId && al.EntityType == entityType)
                .OrderByDescending(al => al.Timestamp)
                .ToListAsync();
        }

        /// <summary>
        /// Получает все действия пользователя за период
        /// </summary>
        public async Task<List<AuditLog>> GetUserActionsAsync(int userId, DateTime startDate, DateTime endDate)
        {
            return await _db.AuditLogs
                .Where(al => al.UserId == userId && al.Timestamp >= startDate && al.Timestamp <= endDate)
                .OrderByDescending(al => al.Timestamp)
                .ToListAsync();
        }

        /// <summary>
        /// Получает логи аудита по типу сущности и фильтрам
        /// </summary>
        public async Task<List<AuditLog>> GetAuditLogByTypeAsync(string entityType, DateTime startDate, DateTime endDate)
        {
            return await _db.AuditLogs
                .Where(al => al.EntityType == entityType && al.Timestamp >= startDate && al.Timestamp <= endDate)
                .OrderByDescending(al => al.Timestamp)
                .Include(al => al.User)
                .ToListAsync();
        }

        /// <summary>
        /// Получает детальную информацию о конкретном логе аудита с разобранным JSON
        /// </summary>
        public async Task<AuditEntryDetailDto?> GetDetailedAuditEntryAsync(int auditLogId)
        {
            var log = await _db.AuditLogs
                .Include(al => al.User)
                .FirstOrDefaultAsync(al => al.AuditLogId == auditLogId);

            if (log == null)
                return null;

            return new AuditEntryDetailDto
            {
                AuditLogId = log.AuditLogId,
                UserId = log.UserId,
                FullName = log.User.FullName,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Action = log.Action,
                OldValues = log.OldValues != null ? JsonSerializer.Deserialize<object>(log.OldValues) : null,
                NewValues = log.NewValues != null ? JsonSerializer.Deserialize<object>(log.NewValues) : null,
                Timestamp = log.Timestamp
            };
        }
    }
}

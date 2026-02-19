using Hangfire;
using Microsoft.EntityFrameworkCore;
using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Extensions;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using SystemTask = System.Threading.Tasks.Task;

namespace PawPoint.Services.Services
{
    public class NotificationService(
        Context dbContext,
        INotificationRealtimeDispatcher realtime,
        IBackgroundJobClient jobs
    ) : INotificationService
    {
        public async Task<PagedResult<NotificationResponse>> GetAllNotificationsAsync(
            int userId,
            bool? isRead,
            int? typeId,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var query = dbContext.Notifications
                .AsNoTracking()
                .Where(n => !n.IsDeleted && n.UserId == userId);

            query = ApplyFilters(query, isRead, typeId);

            var ordered = query
                .OrderBy(n => n.IsRead)
                .ThenByDescending(n => n.CreatedAt);

            var projected = ordered.Select(n => new NotificationResponse(
                n.Id,
                n.Name,
                n.Content,
                n.IsRead,
                n.CreatedAt,
                n.NotificationTypeId,
                n.NotificationType.Name
            ));

            return await projected.ToPagedAsync(pageNumber, pageSize);
        }

        public async SystemTask MarkAsReadAsync(int userId, int notificationId)
        {
            var notification = await dbContext.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == notificationId &&
                    n.UserId == userId &&
                    !n.IsDeleted);

            if (notification == null)
                throw new KeyNotFoundException("Notification not found.");

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                dbContext.Notifications.Update(notification);
                await dbContext.SaveChangesAsync();
            }
        }

        public async SystemTask SoftDeleteNotificationsAsync(int userId, int notificationId)
        {
            var updated = await dbContext.Notifications
                .Where(n =>
                    n.Id == notificationId &&
                    n.UserId == userId &&
                    !n.IsDeleted &&
                    n.IsRead &&
                    n.ReadAt != null &&
                    n.ReadAt <= DateTime.UtcNow.AddDays(-1))
                .ExecuteUpdateAsync(u => u.SetProperty(n => n.IsDeleted, true));

            if (updated == 0)
                throw new KeyNotFoundException("Notification not found");
        }

        /// <summary>
        /// Creează notificarea imediat (DB) + o trimite realtime prin SignalR.
        /// </summary>
        public async SystemTask CreateNotificationAsync(int userId, NotificationCreateRequest request, bool systemRun = false)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (request.UserId != userId) throw new UnauthorizedAccessException("Not allowed.");

            var typeEntity = await dbContext.NotificationTypes
                .FirstAsync(t => t.Name == request.Type.ToString());

            var title = string.IsNullOrWhiteSpace(request.Title)
                ? request.Type.ToString()
                : request.Title.Trim();

            var content = request.Content?.Trim() ?? string.Empty;

            var n = new Notification
            {
                Name = title,
                Content = content,
                UserId = request.UserId,
                NotificationTypeId = typeEntity.Id,
                // CreatedAt = DateTime.UtcNow  // dacă nu ai default în DB/entity
            };

            dbContext.Notifications.Add(n);
            await dbContext.SaveChangesAsync();

            await PushRealtimeAsync(n, request.Type);
        }

        /// <summary>
        /// Programează o notificare la o dată/oră exactă (UTC).
        /// Ex: slot.StartTimeUtc.AddHours(-24), nextDate.Date.AddHours(9), etc.
        /// </summary>
        public SystemTask ScheduleNotificationAsync(int userId, NotificationCreateRequest request, DateTime whenUtc)
        {
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (request.UserId != userId) throw new UnauthorizedAccessException("Not allowed.");

            // Nu programa în trecut -> trimite imediat
            if (whenUtc <= DateTime.UtcNow.AddSeconds(5))
            {
                return CreateNotificationAsync(userId, request, systemRun: true);
            }

            jobs.Schedule<INotificationService>(
                s => s.CreateScheduledNotificationAsync(userId, request),
                whenUtc
            );

            return SystemTask.CompletedTask;
        }

        /// <summary>
        /// Metodă apelată de Hangfire la momentul programat.
        /// </summary>
        [AutomaticRetry(Attempts = 2)]
        public async SystemTask CreateScheduledNotificationAsync(int userId, NotificationCreateRequest request)
        {
            // doar rulează create normal (imediat)
            await CreateNotificationAsync(userId, request, systemRun: true);
        }

        #region Private Methods

        private async SystemTask PushRealtimeAsync(Notification n, NotificationTypeEnum typeEnum)
        {
            // AsNoTracking: n vine tracked, dar e ok; ne trebuie NotificationType.Name pentru response
            // Dacă nu ai navigation loaded, poți face load:
            // await dbContext.Entry(n).Reference(x => x.NotificationType).LoadAsync();

            await realtime.PushToUserAsync(n.UserId, new
            {
                id = n.Id,
                name = n.Name,
                content = n.Content,
                isRead = n.IsRead,
                createdAt = n.CreatedAt,
                typeId = n.NotificationTypeId,
                typeName = typeEnum.ToString()
            });
        }

        private static IQueryable<Notification> ApplyFilters(
            IQueryable<Notification> query,
            bool? isRead,
            int? typeId)
        {
            query = isRead switch
            {
                null => query,
                _ => query.Where(n => n.IsRead == isRead.Value)
            };

            query = typeId switch
            {
                null => query,
                <= 0 => throw new ArgumentException("Notification typeId must be positive."),
                _ => query.Where(n => n.NotificationTypeId == typeId.Value)
            };

            return query;
        }

        #endregion
    }
}

using PawPoint.DB;
using PawPoint.DB.Entities;
using PawPoint.DB.Enums;
using PawPoint.Services.Extensions;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using PawPoint.Services.Responses;
using Microsoft.EntityFrameworkCore;
using SystemTask = System.Threading.Tasks.Task;
using Hangfire;

namespace PawPoint.Services.Services
{
    public class NotificationService(Context dbContext, INotificationRealtimeDispatcher realtime, IBackgroundJobClient jobs) : INotificationService
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

            var invitationName = NotificationTypeEnum.Invitation.ToString().ToLower();

            var ordered = query
                .OrderBy(n => n.IsRead) 
                .ThenByDescending(n => n.NotificationType.Name.ToLower() == invitationName) 
                .ThenByDescending(n => n.CreatedAt);

            var projected = ordered
                .Select(n => new NotificationResponse(
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
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId && !n.IsDeleted);
            
            if (notification == null)
            {
                throw new KeyNotFoundException("Notification not found.");
            }
            
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
            var notification = await dbContext.Notifications
                .Where(n => n.Id == notificationId 
                    && n.UserId == userId
                    && !n.IsDeleted
                    && n.IsRead
                    && n.ReadAt != null
                    && n.ReadAt <= DateTime.UtcNow.AddDays(-1))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(n => n.IsDeleted, true)
                );

            if (notification == 0)
            {
                throw new KeyNotFoundException("Notification not found");
            }
        }

        public async SystemTask CreateNotificationAsync(int userId, NotificationCreateRequest request, bool systemRun = false)
        {
            if (request.Type is NotificationTypeEnum.Alert or NotificationTypeEnum.Invitation)
            {
                var typeEntity = await dbContext.NotificationTypes
                    .FirstAsync(t => t.Name == request.Type.ToString());

                var n = new Notification
                {
                    Name = string.IsNullOrWhiteSpace(request.Title) ? request.Type.ToString() : request.Title,
                    Content = request.Content,
                    UserId = request.UserId,              
                    NotificationTypeId = typeEntity.Id
                };

                dbContext.Notifications.Add(n);
                await dbContext.SaveChangesAsync();

                await realtime.PushToUserAsync(request.UserId, new
                {
                    id = n.Id,
                    n.Name,
                    n.Content,
                    n.IsRead,
                    n.CreatedAt,
                    typeId = n.NotificationTypeId,
                    typeName = request.Type.ToString()
                });

                return;
            }

            if (request.Type is NotificationTypeEnum.Reminder or NotificationTypeEnum.Commercial)
            {
                if (!systemRun)
                {
                    jobs.Schedule<INotificationService>(
                        s => s.CreateNotificationAsync(userId, request, true),
                        NextDay());

                    return;
                }

                var typeEntity = await dbContext.NotificationTypes
                    .FirstAsync(t => t.Name == request.Type.ToString());

                var title = string.IsNullOrWhiteSpace(request.Title)
                    ? request.Type.ToString()
                    : request.Title;

                var content = !string.IsNullOrWhiteSpace(request.Content)
                    ? request.Content
                    : request.Type == NotificationTypeEnum.Reminder
                        ? $"Don't forget about your objectives ({DateTime.UtcNow.Date:yyyy-MM-dd})!"
                        : "Today's offer just dropped!";

                var n = new Notification
                {
                    Name = title,
                    Content = content,
                    UserId = request.UserId,
                    NotificationTypeId = typeEntity.Id
                };

                dbContext.Notifications.Add(n);
                await dbContext.SaveChangesAsync();

                await realtime.PushToUserAsync(request.UserId, new
                {
                    id = n.Id,
                    n.Name,
                    n.Content,
                    n.IsRead,
                    n.CreatedAt,
                    typeId = n.NotificationTypeId,
                    typeName = request.Type.ToString()
                });

                return;
            }

            throw new ArgumentException($"Unknown notification type: {request.Type}");
        }

        #region Private Methods
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

        private static DateTime NextDay()
        {
            var now = DateTime.UtcNow;
            var next = now.Date.AddHours(7); 
            if (now >= next)
            {
                next = next.AddDays(1); 
            }
            return next;
        }
        #endregion
    }
}

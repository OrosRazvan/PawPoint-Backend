using PawPoint.Services.Responses;
using PawPoint.Services.Requests;    

namespace PawPoint.Services.Interfaces
{
    public interface INotificationService
    {
        public Task<PagedResult<NotificationResponse>> GetAllNotificationsAsync(
            int userId,
            bool? isRead,
            int? typeId,
            int pageNumber = 1,
            int pageSize = 20);

        public Task MarkAsReadAsync(int userId, int notificationId);
        public Task SoftDeleteNotificationsAsync(int userId, int notificationId);
        public Task CreateNotificationAsync(int userId, NotificationCreateRequest request, bool systemRun = false);
        public Task ScheduleNotificationAsync(int userId, NotificationCreateRequest request, DateTime whenUtc);
        public Task CreateScheduledNotificationAsync(int userId, NotificationCreateRequest request);

    }
}

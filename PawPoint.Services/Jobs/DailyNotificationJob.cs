using PawPoint.DB;
using PawPoint.DB.Enums;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;
using SystemTask = System.Threading.Tasks.Task;

namespace PawPoint.Services.Jobs
{
    public class DailyNotificationJob(Context db, INotificationService notifications)
    {
        public async SystemTask Run()
        {
            var userIds = db.Users.Select(u => u.Id).ToList();

            foreach (var uid in userIds)
            {
                await notifications.CreateNotificationAsync(
                    uid,
                    new NotificationCreateRequest(
                        NotificationTypeEnum.Reminder,
                        uid,
                        string.Empty,
                        string.Empty),
                    systemRun: true
                );

                await notifications.CreateNotificationAsync(
                    uid,
                    new NotificationCreateRequest(
                        NotificationTypeEnum.Commercial,
                        uid,
                        "Daily Offer",
                        "Today's offer just dropped!"),
                    systemRun: true
                );
            }
        }
    }
}

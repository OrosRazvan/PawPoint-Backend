using Microsoft.AspNetCore.SignalR;
using PawPoint.Services.Interfaces;

namespace PawPoint.Services.Services
{
    public class NotificationRealtimeDispatcher(IHubContext<Hub> hub) : INotificationRealtimeDispatcher
    {
        public Task PushToUserAsync(int userId, object payload)
        {
            return hub.Clients.User(userId.ToString())
                .SendAsync("notification", payload);
        }
    }
}

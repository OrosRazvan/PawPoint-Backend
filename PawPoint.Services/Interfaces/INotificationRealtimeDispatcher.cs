namespace PawPoint.Services.Interfaces
{
    public interface INotificationRealtimeDispatcher
    {
        public Task PushToUserAsync(int userId, object payload);
    }
}

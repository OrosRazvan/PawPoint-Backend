using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PawPoint.ApiServices.Hubs
{
    [Authorize]
    public class NotificationHub : Hub { }
}

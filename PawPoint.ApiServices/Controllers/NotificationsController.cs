using PawPoint.Services.Interfaces;
using PawPoint.Services.Responses;
using PawPoint.Services.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize, Route("notifications")]
    public class NotificationsController(INotificationService _notificationService, IIdentityService identityService) : BaseApiController(identityService)
    {
        [HttpGet]
        public async Task<IActionResult> GetAllNotifications(
            [FromQuery] bool? isRead,
            [FromQuery] int? typeId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserIdFromToken();
            var result = await _notificationService.GetAllNotificationsAsync(userId, isRead, typeId, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpPut("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var userId = GetUserIdFromToken();
            await _notificationService.MarkAsReadAsync(userId, notificationId);
            return Ok();
        }

        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> SoftDeleteNotification(int notificationId)
        {
            var userId = GetUserIdFromToken();
            await _notificationService.SoftDeleteNotificationsAsync(userId, notificationId);
            return Ok(new { Message = "Notification soft deleted successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NotificationCreateRequest request)
        {
            var userId = GetUserIdFromToken();
            await _notificationService.CreateNotificationAsync(userId, request); 
            return Ok(); 
        }
    }
}

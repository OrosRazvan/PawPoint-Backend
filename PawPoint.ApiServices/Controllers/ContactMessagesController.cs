using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [ApiController]
    [Authorize]
    [Route("contact-messages")]
    public class ContactMessagesController(
        IContactMessageService contactMessageService,
        IIdentityService identityService) : BaseApiController(identityService)
    {
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateContactMessageRequest request)
        {
            var userId = GetUserIdFromToken();
            var response = await contactMessageService.CreateAsync(userId, request);
            return Ok(response);
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            var userId = GetUserIdFromToken();
            var response = await contactMessageService.GetMyMessagesAsync(userId);
            return Ok(response);
        }
    }
}
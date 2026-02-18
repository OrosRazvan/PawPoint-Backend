using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize, Route("[controller]")]
    public class FeedingController : BaseApiController
    {
        private readonly IFeedingService _feedingService;

        public FeedingController(
            IFeedingService feedingService,
            IIdentityService identityService)
            : base(identityService)
        {
            _feedingService = feedingService;
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserIdFromToken();
            var result = await _feedingService.GetAllForUserAsync(userId);
            return Ok(result);
        }

        [HttpGet("{feedingId:int}")]
        public async Task<IActionResult> GetById(int feedingId)
        {
            var userId = GetUserIdFromToken();
            var result = await _feedingService.GetByIdAsync(userId, feedingId);
            return Ok(result);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateFeedingRequest request)
        {
            var userId = GetUserIdFromToken();
            var created = await _feedingService.CreateAsync(userId, request);
            return Ok(created);
        }

        [HttpPut("update/{feedingId:int}")]
        public async Task<IActionResult> Update(int feedingId, [FromBody] UpdateFeedingRequest request)
        {
            var userId = GetUserIdFromToken();
            var updated = await _feedingService.UpdateAsync(userId, feedingId, request);
            return Ok(updated);
        }

        [HttpDelete("delete/{feedingId:int}")]
        public async Task<IActionResult> Delete(int feedingId)
        {
            var userId = GetUserIdFromToken();
            await _feedingService.DeleteAsync(userId, feedingId);
            return Ok();
        }
    }
}

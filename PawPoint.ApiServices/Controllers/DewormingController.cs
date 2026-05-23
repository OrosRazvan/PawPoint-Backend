using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize, Route("[controller]")]
    public class DewormingController : BaseApiController
    {
        private readonly IDewormingService _dewormingService;
        private readonly IAppointmentService _appointmentService;

        public DewormingController(
            IDewormingService dewormingService,
            IAppointmentService appointmentService,
            IIdentityService identityService)
            : base(identityService)
        {
            _dewormingService = dewormingService;
            _appointmentService = appointmentService;
        }

        // reuse logic from appointments (same as vaccination controller)
        [HttpGet("vet-cabinets")]
        public async Task<IActionResult> GetVetCabinets(
            [FromQuery] string? serviceType,
            [FromQuery] string? sortBy)
        {
            var result = await _appointmentService.GetVetCabinetsAsync(serviceType, sortBy);
            return Ok(result);
        }

        [HttpGet("availability/{vetCabinetId:int}")]
        public async Task<IActionResult> GetAvailability(
            int vetCabinetId,
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to)
        {
            var userId = GetUserIdFromToken();
            var result = await _appointmentService.GetAvailabilityAsync(userId, vetCabinetId, from, to);
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserIdFromToken();
            var result = await _dewormingService.GetAllForUserAsync(userId);
            return Ok(result);
        }

        [HttpGet("{dewormingId:int}")]
        public async Task<IActionResult> GetById(int dewormingId)
        {
            var userId = GetUserIdFromToken();
            var result = await _dewormingService.GetByIdAsync(userId, dewormingId);
            return Ok(result);
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateDewormingRequest request)
        {
            var userId = GetUserIdFromToken();
            var created = await _dewormingService.CreateAsync(userId, request);
            return Ok(created);
        }

        [HttpPut("update/{dewormingId:int}")]
        public async Task<IActionResult> Update(int dewormingId, [FromBody] UpdateDewormingRequest request)
        {
            var userId = GetUserIdFromToken();
            var updated = await _dewormingService.UpdateAsync(userId, dewormingId, request);
            return Ok(updated);
        }

        [HttpDelete("delete/{dewormingId:int}")]
        public async Task<IActionResult> Delete(int dewormingId)
        {
            var userId = GetUserIdFromToken();
            await _dewormingService.DeleteAsync(userId, dewormingId);
            return Ok();
        }
    }
}

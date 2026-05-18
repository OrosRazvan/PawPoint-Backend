using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PawPoint.Services.Interfaces;
using PawPoint.Services.Requests;

namespace PawPoint.ApiServices.Controllers
{
    [Authorize, Route("[controller]")]
    public class AppointmentController : BaseApiController
    {
        private readonly IAppointmentService _appointmentService;

        public AppointmentController(
            IAppointmentService appointmentService,
            IIdentityService identityService)
            : base(identityService)
        {
            _appointmentService = appointmentService;
        }

        // GET /Appointment/vet-cabinets?serviceType=Vaccination&sort=price
        [HttpGet("vet-cabinets")]
        public async Task<IActionResult> GetVetCabinets(
            [FromQuery] string? serviceType,
            [FromQuery] string? sortBy)
        {
            var result = await _appointmentService.GetVetCabinetsAsync(serviceType, sortBy);
            return Ok(result);
        }

        // GET /Appointment/availability/5?from=2025-08-01&to=2025-08-31
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
            var result = await _appointmentService.GetAllForUserAsync(userId);
            return Ok(result);
        }

        [HttpGet("{appointmentId:int}")]
        public async Task<IActionResult> GetById(int appointmentId)
        {
            var userId = GetUserIdFromToken();
            var result = await _appointmentService.GetByIdAsync(userId, appointmentId);
            return Ok(result);
        }

        // POST /Appointment/create
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
        {
            var userId = GetUserIdFromToken(); // la fel ca în AnimalController :contentReference[oaicite:3]{index=3}
            var appointment = await _appointmentService.CreateAsync(userId, request);
            return Ok(appointment);
        }

        [HttpPut("update/{appointmentId:int}")]
        public async Task<IActionResult> Update(
            int appointmentId,
            [FromBody] UpdateAppointmentRequest request)
        {
            var userId = GetUserIdFromToken();
            var updated = await _appointmentService.UpdateAsync(appointmentId, userId, request);
            return Ok(updated);
        }
    }
}
